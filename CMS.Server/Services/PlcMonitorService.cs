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
            _logger.LogInformation("PlcMonitorService ExecuteAsync started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _iterationCount++;

                    if (_iterationCount % 60 == 0)
                    {
                        _logger.LogInformation("PlcMonitorService heartbeat - Iteration: {Count}, Time: {Time}",
                        _iterationCount, DateTime.Now);
                    }

                    var plcResults = _plcService.ReadAllPlcs();
                    var time = DateTime.Now;

                    if (!plcResults.TryGetValue("Data", out var dataObj) || dataObj is not Dictionary<string, object> combinedData)
                    {
                        _logger.LogWarning("Missing or invalid Data from PLC at {Time}", time);
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    var dRaw = combinedData.GetValueOrDefault("D_RAW") as byte[] ?? Array.Empty<byte>();
                    var wRaw = combinedData.GetValueOrDefault("W_RAW") as bool[] ?? Array.Empty<bool>();

                    for (int i = 0; i < 26; i++)
                    {
                        int Doffset = i * 500;
                        int Woffset = i * 3;

                        // Read D Memory values
                        int id_machine = i + 1;
                        int shot = Math.Min(ReadIntFromD(dRaw, 30 + Doffset), 10000);
                        int shot_accum = Math.Min(ReadIntFromD(dRaw, 32 + Doffset), 10000);
                        float act_ct = Math.Min(ReadFloatFromD(dRaw, 60 + Doffset), 1000f);
                        int mould_category_no = Math.Min(ReadIntFromD(dRaw, 90 + Doffset), 10);
                        string stop_category = ReadStringFromD(dRaw, 300 + Doffset);
                        string remark = ReadStringFromD(dRaw, 400 + Doffset);

                        float reject_panelling = Math.Min(ReadFloatFromD(dRaw, 250 + Doffset), 10000f);
                        float reject_lumpy = Math.Min(ReadFloatFromD(dRaw, 255 + Doffset), 10000f);
                        float reject_black_dot = Math.Min(ReadFloatFromD(dRaw, 260 + Doffset), 10000f);
                        float reject_burst = Math.Min(ReadFloatFromD(dRaw, 265 + Doffset), 10000f);
                        float reject_startup = Math.Min(ReadFloatFromD(dRaw, 270 + Doffset), 10000f);
                        float reject_preform = Math.Min(ReadFloatFromD(dRaw, 275 + Doffset), 10000f);
                        float reject_purging = Math.Min(ReadFloatFromD(dRaw, 280 + Doffset), 10000f);
                        float reject_others = Math.Min(ReadFloatFromD(dRaw, 285 + Doffset), 10000f);

                        // Read W Memory values (bits)
                        bool status_start = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 0);
                        bool status_off = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 1);
                        bool production_running = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 2);
                        bool visual_qc = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 3);
                        bool remark_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 5);
                        bool reject_signal = ReadBitFromW(wRaw, (ushort)(0 + Woffset), 6);

                        bool util_barrel = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 0);
                        bool util_hyd_motor = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 1);
                        bool util_dehumidifier = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 2);
                        bool util_chiller = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 3);
                        bool util_material = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 4);
                        bool util_dry_cycle = ReadBitFromW(wRaw, (ushort)(1 + Woffset), 5);

                        var plcData = new
                        {
                            id_machine = id_machine,
                            time = time,
                            shot = shot,
                            shot_accum = shot_accum,
                            act_ct = act_ct,
                            mould_category_no = mould_category_no,
                            stop_category = stop_category,
                            remark = remark,
                            reject_panelling = reject_panelling,
                            reject_lumpy = reject_lumpy,
                            reject_black_dot = reject_black_dot,
                            reject_burst = reject_burst,
                            reject_startup = reject_startup,
                            reject_preform = reject_preform,
                            reject_purging = reject_purging,
                            reject_others = reject_others,
                            status_start = status_start,
                            status_off = status_off,
                            production_running = production_running,
                            visual_qc = visual_qc,
                            remark_signal = remark_signal,
                            reject_signal = reject_signal,
                            util_barrel = util_barrel,
                            util_hyd_motor = util_hyd_motor,
                            util_dehumidifier = util_dehumidifier,
                            util_chiller = util_chiller,
                            util_material = util_material,
                            util_dry_cycle = util_dry_cycle
                        };

                        await _dbService.insertMachineMaster(plcData);

                        // DisplayMasterData(plcData, i);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("PlcMonitorService operation was cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PlcMonitorService at iteration {Count}: {Message}",
                    _iterationCount, ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("PlcMonitorService delay was cancelled");
                    break;
                }
            }

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

            //Console.WriteLine($"Status          : Start=W{5 + Woffset}.0 ({plcData.status_start}), " +
            //                  $"Off=W{5 + Woffset}.1 ({plcData.status_off}), " +
            //                  $"Visual_QC=W{5 + Woffset}.3 ({plcData.visual_qc}), " +
            //                  $"Remark Signal=W{5 + Woffset}.5 ({plcData.remark_signal}), " +
            //                  $"Reject Signal=W{5 + Woffset}.6 ({plcData.reject_signal})");

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
