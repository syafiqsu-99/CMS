using System.Text;

namespace CMS.server.Services
{
    public class PlcMonitorService : BackgroundService
    {
        private readonly PlcService _plcService;
        private readonly MachineLogService _dbService;
        private readonly ILogger<PlcMonitorService> _logger;
        private int _iterationCount = 0;

        public PlcMonitorService(PlcService plcService, MachineLogService dbService, ILogger<PlcMonitorService> logger)
        {
            _plcService = plcService;
            _dbService = dbService;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PlcMonitorService is starting at {Time}", DateTime.Now);
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("PlcMonitorService is stopping at {Time}", DateTime.Now);
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {
                    _iterationCount++;

                    if (_iterationCount % 200 == 0)
                        _logger.LogInformation("Heartbeat - Iteration: {Count}, Time: {Time}", _iterationCount, DateTime.Now);

                    var plcResults = _plcService.ReadAllPlcs();
                    var time = DateTime.Now;

                    if (!plcResults.TryGetValue("Data", out var dataObj) || dataObj is not Dictionary<string, object> combinedData)
                    {
                        _logger.LogWarning("Missing or invalid Data from PLC at {Time}", time);
                        continue;
                    }

                    var dRaw = combinedData.GetValueOrDefault("D_RAW") as byte[] ?? Array.Empty<byte>();
                    var wRaw = combinedData.GetValueOrDefault("W_RAW") as bool[] ?? Array.Empty<bool>();

                    var allPlcData = new List<object>(26);
                    for (int i = 0; i < 26; i++)
                    {
                        int Doffset = i * 500;
                        int Woffset = i * 3;

                        allPlcData.Add(new
                        {
                            // Read D Memory values
                            id_machine = i + 1,
                            time = time,
                            shot = Math.Min(ReadIntFromD(dRaw, 30 + Doffset), 10000),
                            shot_accum = Math.Min(ReadIntFromD(dRaw, 32 + Doffset), 10000),
                            act_ct = Math.Min(ReadFloatFromD(dRaw, 60 + Doffset), 1000f),
                            mould_category_no = Math.Min(ReadIntFromD(dRaw, 90 + Doffset), 10),
                            stop_category = ReadStringFromD(dRaw, 300 + Doffset),
                            remark = ReadStringFromD(dRaw, 400 + Doffset),

                            reject_panelling = Math.Min(ReadFloatFromD(dRaw, 250 + Doffset), 10000f),
                            reject_lumpy = Math.Min(ReadFloatFromD(dRaw, 255 + Doffset), 10000f),
                            reject_black_dot = Math.Min(ReadFloatFromD(dRaw, 260 + Doffset), 10000f),
                            reject_burst = Math.Min(ReadFloatFromD(dRaw, 265 + Doffset), 10000f),
                            reject_startup = Math.Min(ReadFloatFromD(dRaw, 270 + Doffset), 10000f),
                            reject_preform = Math.Min(ReadFloatFromD(dRaw, 275 + Doffset), 10000f),
                            reject_purging = Math.Min(ReadFloatFromD(dRaw, 280 + Doffset), 10000f),
                            reject_others = Math.Min(ReadFloatFromD(dRaw, 285 + Doffset), 10000f),

                            // Read W Memory values (bits)
                            status_start = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 0),
                            status_off = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 1),
                            production_running = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 2),
                            visual_qc = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 3),
                            done = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 4),
                            remark_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 5),
                            reject_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 6),

                            util_barrel = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 0),
                            util_hyd_motor = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 1),
                            util_dehumidifier = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 2),
                            util_chiller = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 3),
                            util_material = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 4),
                            util_dry_cycle = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 5),
                        });
                        //DisplayMasterData(allPlcData[i], i);
                    }

                    await Task.WhenAll(allPlcData.Select(p => _dbService.insertMachineMaster(p)));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PlcMonitorService at iteration {Count}", _iterationCount);
                }
            }

            await Task.Delay(200, stoppingToken);

            _logger.LogWarning("PlcMonitorService ExecuteAsync loop ended. Total iterations: {Count}", _iterationCount);
        }
        private int ReadIntFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buffer.Length) return 0;
            byte[] reordered = { buffer[byteIndex + 1], buffer[byteIndex], buffer[byteIndex + 3], buffer[byteIndex + 2] };
            int value = BitConverter.ToInt32(reordered, 0);
            return value;
        }
        private float ReadFloatFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;
            if (byteIndex + 4 > buffer.Length) return 0f;

            byte[] reordered = { buffer[byteIndex + 1], buffer[byteIndex], buffer[byteIndex + 3], buffer[byteIndex + 2] };
            float value = BitConverter.ToSingle(reordered, 0);

            return value;
        }
        private string ReadStringFromD(byte[] buffer, int wordIndex)
        {
            int byteIndex = wordIndex * 2;

            if (byteIndex >= buffer.Length) return string.Empty;

            if (buffer[byteIndex] == 0) return string.Empty;

            return Encoding.ASCII.GetString(buffer, byteIndex, 100).Trim('\0', ' ');
        }
        private bool ReadBitFromW(bool[] buffer, int wordIndex, int bit)
        {
            int index = wordIndex * 16 + bit;
            if (index < 0 || index >= buffer.Length) return false;
            return buffer[index];
        }
        private void DisplayMasterData(dynamic plcData, int index)
        {
            int Doffset = index * 500;
            int Woffset = index * 3;

            Console.WriteLine($"=== Master.{plcData.id_machine} Data ===");
            Console.WriteLine($"Machine Info    : ID={plcData.id_machine}");

            //Console.WriteLine($"Production      : Shot=D{530 + Doffset} ({plcData.shot}), Shot_Accum=D{532 + Doffset} ({plcData.shot_accum}), " +
            //                  $"CT=D{560 + Doffset} ({plcData.act_ct:F2}s)");

            Console.WriteLine($"Status          : Start=W{5 + Woffset}.0 ({plcData.status_start}), " +
                              $"Off=W{5 + Woffset}.1 ({plcData.status_off}), " +
                              $"Visual_QC=W{5 + Woffset}.3 ({plcData.visual_qc}), " +
                              $"Remark Signal=W{5 + Woffset}.5 ({plcData.remark_signal}), " +
                              $"Reject Signal=W{5 + Woffset}.6 ({plcData.reject_signal})");

            //Console.WriteLine($"Quality         : Visual_QC=W{5 + Woffset}.3 ({plcData.visual_qc}), " +
            //                  $"Measure_QC=W{7 + Woffset}.0 ({plcData.measure_qc})");

            //Console.WriteLine($"Rejects         : Panelling=D{750 + Doffset} ({plcData.reject_panelling:F1}), " +
            //                  $"Lumpy=D{755 + Doffset} ({plcData.reject_lumpy:F1}), " +
            //                  $"Black Dot=D{760 + Doffset} ({plcData.reject_black_dot:F1}), " +
            //                  $"Burst=D{765 + Doffset} ({plcData.reject_burst:F1}), " +
            //                  $"Startup=D{770 + Doffset} ({plcData.reject_startup:F1}), " +
            //                  $"Preform=D{775 + Doffset} ({plcData.reject_preform:F1}), " +
            //                  $"Purging=D{780 + Doffset} ({plcData.reject_purging:F1}), " +
            //                  $"Others=D{785 + Doffset} ({plcData.reject_others:F1})");

            //Console.WriteLine($"Utilities       : Barrel=W{6 + Woffset}.0 ({plcData.util_barrel}), " +
            //                  $"Motor=W{6 + Woffset}.1 ({plcData.util_hyd_motor}), " +
            //                  $"Dehum=W{6 + Woffset}.2 ({plcData.util_dehumidifier}), " +
            //                  $"Chiller=W{6 + Woffset}.3 ({plcData.util_chiller}), " +
            //                  $"Material=W{6 + Woffset}.4 ({plcData.util_material}), " +
            //                  $"Dry Cycle=W{6 + Woffset}.5 ({plcData.util_dry_cycle}), ");

            Console.WriteLine($"Remark=D{900 + Doffset} ({plcData.remark}), " +
                              $"StopCat=D{800 + Doffset} ({plcData.stop_category})");

            Console.WriteLine($"Time            : {plcData.time:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }
    }
}
