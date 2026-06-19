using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;

namespace CMS.Server.Services
{
    public sealed class SubPlcService
    {
        private static readonly ConcurrentDictionary<string, PlcOmron> _connections = new();
        private readonly ILogger<SubPlcService> _logger;
        private readonly MainPlcService _mainPlc;

        // D memory layout: D10–D776 (767 words)
        private const ushort DStart = 10;
        private const ushort DEnd = 776;
        private const int DTotal = DEnd - DStart + 1;
        private const int DMaxChunk = 400;

        // W memory layout: W0–W77 (78 words = 1248 bits)
        private const ushort WStart = 0;
        private const int WTotalWords = 78;
        private const int WTotalBits = WTotalWords * 16;
        private const int WMaxChunk = 480;

        // H memory layout: H0–H37 (38 words)
        private const ushort HStart = 0;
        private const int HTotal = 38;

        // OUT memory: read O100.00 (word 100, bit 0)
        private const ushort OutWord = 100;
        private const MemoryAreaBits OutBitArea = (MemoryAreaBits)0x89;

        public SubPlcService(MainPlcService mainPlc, ILogger<SubPlcService> logger)
        {
            _mainPlc = mainPlc;
            _logger = logger;
        }

        // ── Public: read all machines on demand ────────────────────────────────

        public async Task<Dictionary<string, object?>> ReadAllAsync()
        {
            var machines = await _mainPlc.GetCachedMachinesAsync();
            var result = new Dictionary<string, object?>();

            await Parallel.ForEachAsync(machines, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (machine, _) =>
            {
                var data = await ReadOneAsync(machine.id, machine.name);
                lock (result) result[machine.id.ToString()] = data;
            });

            return result;
        }

        // ── Public: read a single sub-PLC by id (fetches machine name from cache) ──
        public async Task<Dictionary<string, object?>> ReadOneAsync(int id)
        {
            var machines = await _mainPlc.GetCachedMachinesAsync();
            var machine = machines.FirstOrDefault(m => m.id == id);
            return await ReadOneAsync(id, machine.name ?? string.Empty);
        }

        // ── Internal: read a single sub-PLC ───────────────────────────────────

        private Task<Dictionary<string, object?>> ReadOneAsync(int id, string machineName)
        {
            return Task.Run(() =>
            {
                var ip = $"172.17.86.{220 + id}";
                var cacheKey = $"sub_{ip}";
                var result = new Dictionary<string, object?> { ["machine_name"] = machineName };
                PlcOmron? plc = null;

                try
                {
                    if (!_connections.TryGetValue(cacheKey, out plc) || plc == null)
                    {
                        byte remoteNode = (byte)(220 + id);
                        plc = new PlcOmron(ip, 9600, false, remoteNode, 136);
                        _connections[cacheKey] = plc;
                    }

                    plc.Connect();

                    bool coreOk = ReadDMemory(id, plc, result)
                               && ReadWMemory(id, plc, result)
                               && ReadHMemory(id, plc, result);

                    ReadOutMemory(id, plc, result);

                    result["online"] = coreOk;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[SubPlc M{Id}] Offline or read failed: {Msg}", id, ex.Message);
                    _connections.TryRemove(cacheKey, out _);
                    if (plc != null) try { plc.Disconnect(); } catch { }
                    result["online"] = false;
                }

                return result;
            });
        }

        // ── D memory ──────────────────────────────────────────────────────────

        private bool ReadDMemory(int id, PlcOmron plc, Dictionary<string, object?> result)
        {
            byte[] dBuf = new byte[DTotal * 2];

            for (int offset = 0; offset < DTotal; offset += DMaxChunk)
            {
                ushort chunkStart = (ushort)(DStart + offset);
                ushort chunkSize = (ushort)Math.Min(DMaxChunk, DTotal - offset);

                byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);
                if (chunk == null || chunk.Length < chunkSize * 2)
                {
                    _logger.LogWarning("[SubPlc M{Id}] D short read at D{Addr}", id, chunkStart);
                    return false;
                }

                Buffer.BlockCopy(chunk, 0, dBuf, offset * 2, chunk.Length);
            }

            int Bidx(int dWord) => (dWord - DStart) * 2;

            // D10 – Visual QC count
            result["D10"] = ReadInt(dBuf, Bidx(10));
            // D12 – Measure QC count
            result["D12"] = ReadInt(dBuf, Bidx(12));
            // D35 – Part weight (float, grams)
            result["D35"] = ReadFloat(dBuf, Bidx(35));
            // D40 – Shot count this run
            result["D40"] = ReadInt(dBuf, Bidx(40));
            // D42 – Shot count others
            result["D42"] = ReadInt(dBuf, Bidx(42));
            // D44 – Cycle time accumulator (float)
            result["D44"] = ReadFloat(dBuf, Bidx(44));
            // D48 – Total shot accumulator
            result["D48"] = ReadInt(dBuf, Bidx(48));
            // D50 – Accumulated shot count (persistent)
            result["D50"] = ReadInt(dBuf, Bidx(50));
            // D70 – Last cycle time (float, seconds)
            result["D70"] = ReadFloat(dBuf, Bidx(70));
            // D80 – Accumulated cycle time (float)
            result["D80"] = ReadFloat(dBuf, Bidx(80));
            // D90 – Actual cycle time average (float, seconds)
            result["D90"] = ReadFloat(dBuf, Bidx(90));
            // D110 – Stop category number
            result["D110"] = ReadInt(dBuf, Bidx(110));
            // D115 – Sub-stop category number
            result["D115"] = ReadInt(dBuf, Bidx(115));
            // D120 – Mould change category number
            result["D120"] = ReadInt(dBuf, Bidx(120));
            // D180 – HMI stop category colour
            result["D180"] = ReadInt(dBuf, Bidx(180));
            // D190 – HMI page number
            result["D190"] = ReadInt(dBuf, Bidx(190));
            // D200 – Product type / model string (100 bytes)
            result["D200"] = ReadString(dBuf, Bidx(200), 100);
            // D300 – Packer name string (100 bytes)
            result["D300"] = ReadString(dBuf, Bidx(300), 100);
            // D400 – Stop category string (100 bytes)
            result["D400"] = ReadString(dBuf, Bidx(400), 100);
            // D500 – Remark / problem string (100 bytes)
            result["D500"] = ReadString(dBuf, Bidx(500), 100);

            // D600–D675: current-shot reject kg / pcs
            result["D600"] = ReadFloat(dBuf, Bidx(600));
            result["D605"] = ReadFloat(dBuf, Bidx(605));
            result["D610"] = ReadFloat(dBuf, Bidx(610));
            result["D615"] = ReadFloat(dBuf, Bidx(615));
            result["D620"] = ReadFloat(dBuf, Bidx(620));
            result["D625"] = ReadFloat(dBuf, Bidx(625));
            result["D630"] = ReadFloat(dBuf, Bidx(630));
            result["D635"] = ReadFloat(dBuf, Bidx(635));
            result["D640"] = ReadFloat(dBuf, Bidx(640));
            result["D645"] = ReadFloat(dBuf, Bidx(645));
            result["D650"] = ReadFloat(dBuf, Bidx(650));
            result["D655"] = ReadFloat(dBuf, Bidx(655));
            result["D660"] = ReadFloat(dBuf, Bidx(660));
            result["D665"] = ReadFloat(dBuf, Bidx(665));
            result["D670"] = ReadFloat(dBuf, Bidx(670));
            result["D675"] = ReadFloat(dBuf, Bidx(675));

            // D700–D735: accumulated totals (kg)
            result["D700"] = ReadFloat(dBuf, Bidx(700));
            result["D705"] = ReadFloat(dBuf, Bidx(705));
            result["D710"] = ReadFloat(dBuf, Bidx(710));
            result["D715"] = ReadFloat(dBuf, Bidx(715));
            result["D720"] = ReadFloat(dBuf, Bidx(720));
            result["D725"] = ReadFloat(dBuf, Bidx(725));
            result["D730"] = ReadFloat(dBuf, Bidx(730));
            result["D735"] = ReadFloat(dBuf, Bidx(735));

            // D740–D775: accumulated totals (pcs)
            result["D740"] = ReadFloat(dBuf, Bidx(740));
            result["D745"] = ReadFloat(dBuf, Bidx(745));
            result["D750"] = ReadFloat(dBuf, Bidx(750));
            result["D755"] = ReadFloat(dBuf, Bidx(755));
            result["D760"] = ReadFloat(dBuf, Bidx(760));
            result["D765"] = ReadFloat(dBuf, Bidx(765));
            result["D770"] = ReadFloat(dBuf, Bidx(770));
            result["D775"] = ReadFloat(dBuf, Bidx(775));

            return true;
        }

        // ── W memory ──────────────────────────────────────────────────────────

        private bool ReadWMemory(int id, PlcOmron plc, Dictionary<string, object?> result)
        {
            byte[] wBuf = new byte[WTotalBits];

            for (int offset = 0; offset < WTotalBits; offset += WMaxChunk)
            {
                ushort chunkSize = (ushort)Math.Min(WMaxChunk, WTotalBits - offset);
                ushort chunkStartWord = (ushort)(WStart + offset / 16);

                byte[] chunk = plc.Read(chunkStartWord, chunkSize, 0, MemoryAreaBits.Work);
                if (chunk == null || chunk.Length < chunkSize)
                {
                    _logger.LogWarning("[SubPlc M{Id}] W short read at W{Word}.0", id, chunkStartWord);
                    return false;
                }

                Buffer.BlockCopy(chunk, 0, wBuf, offset, chunk.Length);
            }

            bool Wbit(int word, int bit)
            {
                int idx = (word - WStart) * 16 + bit;
                return idx >= 0 && idx < wBuf.Length && wBuf[idx] != 0;
            }

            result["W0.00"] = Wbit(0, 0);   // Power supply signal
            result["W1.00"] = Wbit(1, 0);   // Start auto signal
            result["W2.00"] = Wbit(2, 0);   // Production running (local)
            result["W3.00"] = Wbit(3, 0);   // Shot pulse signal
            result["W4.00"] = Wbit(4, 0);   // CT timer signal
            result["W5.00"] = Wbit(5, 0);   // Change shift signal
            result["W5.01"] = Wbit(5, 1);   // Reset signal
            result["W6.00"] = Wbit(6, 0);   // Remark signal
            result["W7.00"] = Wbit(7, 0);   // Reject signal
            result["W8.00"] = Wbit(8, 0);   // Stop category signal
            result["W9.00"] = Wbit(9, 0);   // QC signal

            result["W20.00"] = Wbit(20, 0);  // Central: status start
            result["W20.01"] = Wbit(20, 1);  // Central: status off
            result["W20.02"] = Wbit(20, 2);  // Central: production running
            result["W20.03"] = Wbit(20, 3);  // Central: QC signal
            result["W20.04"] = Wbit(20, 4);  // Central: done signal
            result["W20.05"] = Wbit(20, 5);  // Central: remark signal
            result["W20.06"] = Wbit(20, 6);  // Central: reject signal

            result["W30.00"] = Wbit(30, 0);  // HMI done

            result["W60.00"] = Wbit(60, 0);  // Barrel
            result["W60.01"] = Wbit(60, 1);  // Hydraulic motor
            result["W60.02"] = Wbit(60, 2);  // Dehumidifier
            result["W60.03"] = Wbit(60, 3);  // Chiller
            result["W60.04"] = Wbit(60, 4);  // Material
            result["W60.05"] = Wbit(60, 5);  // Dry cycle

            result["W70.00"] = Wbit(70, 0);  // Alarm: barrel
            result["W71.00"] = Wbit(71, 0);  // Alarm: hyd. motor
            result["W72.00"] = Wbit(72, 0);  // Alarm: dehumidifier
            result["W73.00"] = Wbit(73, 0);  // Alarm: chiller
            result["W74.00"] = Wbit(74, 0);  // Alarm: material
            result["W75.00"] = Wbit(75, 0);  // Alarm: general
            result["W76.00"] = Wbit(76, 0);  // Alarm: utility
            result["W77.00"] = Wbit(77, 0);  // Reset alarm

            return true;
        }

        // ── H memory ──────────────────────────────────────────────────────────

        private bool ReadHMemory(int id, PlcOmron plc, Dictionary<string, object?> result)
        {
            const MemoryAreaBits HArea = (MemoryAreaBits)0xB2;
 
            byte[] hBuf = plc.Read(HStart, (ushort)HTotal, 0, HArea);
            if (hBuf == null || hBuf.Length < HTotal * 2)
            {
                _logger.LogWarning("[SubPlc M{Id}] H short read: expected {Exp} bytes, got {Got}",
                    id, HTotal * 2, hBuf?.Length ?? 0);
                return false;
            }
 
            int Hidx(int hWord) => hWord * 2;
 
            result["H5"]  = ReadFloat(hBuf, Hidx(5));   // CT constant (0.02s)
            result["H10"] = ReadFloat(hBuf, Hidx(10));  // SAP cycle time
            result["H15"] = ReadFloat(hBuf, Hidx(15));  // Zero float constant
            result["H20"] = ReadInt(hBuf, Hidx(20));    // IP node number
            result["H25"] = ReadInt(hBuf, Hidx(25));    // IP node (main PLC)
            result["H30"] = ReadInt(hBuf, Hidx(30));    // Production password
            result["H32"] = ReadInt(hBuf, Hidx(32));    // Technician password
            result["H34"] = ReadInt(hBuf, Hidx(34));    // Maintenance password
            result["H36"] = ReadInt(hBuf, Hidx(36));    // QC password
 
            return true;
        }

        // ── Read helpers (reuse MainPlcService byte-swap logic) ────────────────
        private void ReadOutMemory(int id, PlcOmron plc, Dictionary<string, object?> result)
        {
            try
            {
                byte[] chunk = plc.Read(OutWord, 1, 0, OutBitArea);
                result["O100.00"] = chunk != null && chunk.Length >= 1 && chunk[0] != 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[SubPlc M{Id}] OUT read failed (non-critical): {Msg}", id, ex.Message);
                result["O100.00"] = false;
            }
        }
        private static int ReadInt(byte[] buf, int idx) => MainPlcService.ReadInt(buf, idx);
        private static float ReadFloat(byte[] buf, int idx) => MainPlcService.ReadFloat(buf, idx);
        private static string ReadString(byte[] buf, int idx, int n) => MainPlcService.ReadString(buf, idx, n);
    }
}