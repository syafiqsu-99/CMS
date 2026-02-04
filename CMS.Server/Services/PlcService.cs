using CMS.Server.Models;
using PLC_Omron_Standard;
using PLC_Omron_Standard.Enums;
using System.Collections.Concurrent;
using System.Text;

namespace CMS.server.Services
{
    public class PlcService
    {
        private static readonly ConcurrentDictionary<string, PlcOmron> _plcConnections = new();

        public Dictionary<string, object> ReadAllPlcs()
        {
            var results = new Dictionary<string, object>();
            var ip = $"172.17.86.80";
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ip, out plc) || plc == null)
                {
                    plc = new PlcOmron(ip, 9600, false, 80, 136);
                    _plcConnections[ip] = plc;
                }

                plc.Connect();

                var combinedData = new Dictionary<string, object>();

                ushort dStart = 500;
                ushort dEnd = 13500;
                int totalDWords = dEnd - dStart + 1;
                int maxDWords = 1000;

                byte[] dBuffer = new byte[totalDWords * 2];

                try
                {
                    for (int offset = 0; offset < totalDWords; offset += maxDWords)
                    {
                        ushort chunkStart = (ushort)(dStart + offset);
                        int remaining = totalDWords - offset;
                        ushort chunkSize = (ushort)Math.Min(maxDWords, remaining);

                        byte[] chunk = plc.Read(chunkStart, chunkSize, 0, MemoryAreaBits.DataMemory);

                        int byteDestIndex = offset * 2;
                        Buffer.BlockCopy(chunk, 0, dBuffer, byteDestIndex, chunk.Length);
                    }

                    combinedData["D_RAW"] = dBuffer;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error batch reading D area: {ex.Message}");
                    combinedData["D_RAW"] = Array.Empty<byte>();
                }

                ushort wStart = 5;
                ushort wEnd = 81;
                int totalWWords = wEnd - wStart + 1;
                int totalBits = totalWWords * 16;

                bool[] wBits = new bool[totalBits];

                try
                {
                    byte[] chunk = plc.Read(wStart, (ushort)totalBits, 0, MemoryAreaBits.Work);
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        wBits[i] = chunk[i] != 0;
                    }

                    combinedData["W_RAW"] = wBits;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error batch reading W area: {ex.Message}");
                    combinedData["W_RAW"] = Array.Empty<bool>();
                }

                results["Data"] = combinedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Critical PLC Connection Error: {ex.Message}");
                Console.WriteLine($"[PlcService] Stack trace: {ex.StackTrace}");

                if (plc != null)
                {
                    _plcConnections.TryRemove(ip, out _);
                    try { plc.Disconnect(); }
                    catch (Exception disconnectEx)
                    {
                        Console.WriteLine($"[PlcService] Error during disconnect: {disconnectEx.Message}");
                    }
                }
            }

            return results;
        }

        public void UpdatePLCS(dynamic master)
        {
            var ip = "172.17.86.80";
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ip, out plc) || plc == null)
                {
                    plc = new PlcOmron(ip, 9600, false, 80, 1);
                    _plcConnections[ip] = plc;
                }
                plc.Connect();

                int baseOffset = (master.id_machine - 1) * 500;
                int ipNode = 220 + (master.id_machine - 1);
                WriteIntOmron(plc, 10, ipNode);

                ushort addrPartWeight = (ushort)(525 + baseOffset);
                WriteFloatOmron(plc, 25, master.part_weight);
                WriteFloatOmron(plc, addrPartWeight, master.part_weight);

                ushort addrType = (ushort)(600 + baseOffset);
                WriteStringOmron(plc, 100, master.type);
                WriteStringOmron(plc, addrType, master.type);

                ushort addrPacker = (ushort)(700 + baseOffset);
                WriteStringOmron(plc, 200, master.packer);
                WriteStringOmron(plc, addrPacker, master.packer);

                WriteBoolOmron(plc, 4, 0, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Error in UpdatePLCS: {ex.Message}");
            }
        }
        public void UpdatePacker(dynamic staffList)
        {
            var ip = "172.17.86.80";
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ip, out plc) || plc == null)
                {
                    plc = new PlcOmron(ip, 9600, false, 80, 1);
                    _plcConnections[ip] = plc;
                }
                plc.Connect();

                foreach (var staff in staffList)
                {
                    int baseOffset = (staff.id_machine - 1) * 500;
                    int ipNode = 220 + (staff.id_machine - 1);

                    WriteIntOmron(plc, 10, ipNode);

                    ushort addrPacker = (ushort)(700 + baseOffset);
                    WriteStringOmron(plc, 200, staff.packer);
                    WriteStringOmron(plc, addrPacker, staff.packer);
                    WriteBoolOmron(plc, 4, 2, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Error in UpdatePLCS: {ex.Message}");
            }
        }
        public void UpdateMeasureQC(machine_master master)
        {
            var ip = "172.17.86.80";
            PlcOmron? plc = null;

            try
            {
                if (!_plcConnections.TryGetValue(ip, out plc) || plc == null)
                {
                    plc = new PlcOmron(ip, 9600, false, 80, 1);
                    _plcConnections[ip] = plc;
                }
                plc.Connect();

                int baseOffset = (master.id_machine - 1) * 3;
                int ipNode = 220 + (master.id_machine - 1);

                WriteIntOmron(plc, 20, ipNode);

                ushort addrMeasureQC = (ushort)(7 + baseOffset);
                WriteBoolOmron(plc, 3, 0, master.measure_qc);
                WriteBoolOmron(plc, addrMeasureQC, 0, master.measure_qc);

                WriteBoolOmron(plc, 3, 1, true);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlcService] Error in UpdateMeasureQC: {ex.Message}");
            }
        }

        private static int ReadIntOmron(PlcOmron plc, ushort address)
        {
            var raw = plc.Read(address, 2); // 4 bytes

            byte[] reorderedBytes = new byte[] { raw[1], raw[0], raw[3], raw[2] };

            return BitConverter.ToInt32(reorderedBytes, 0);
        }

        private static float ReadFloatOmron(PlcOmron plc, ushort address)
        {
            var raw = plc.Read(address, 2); // 4 bytes

            byte[] reorderedBytes = new byte[] { raw[1], raw[0], raw[3], raw[2] };

            return BitConverter.ToSingle(reorderedBytes, 0);
        }

        private static bool WriteIntOmron(PlcOmron plc, ushort address, int value)
        {
            var bytes = BitConverter.GetBytes(value);

            byte[] reorderedBytes = new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] };

            return plc.Write(address, reorderedBytes, 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteFloatOmron(PlcOmron plc, ushort address, float value)
        {
            var bytes = BitConverter.GetBytes(value);

            byte[] reorderedBytes = new byte[] { bytes[1], bytes[0], bytes[3], bytes[2] };

            return plc.Write(address, reorderedBytes, 0, 2, MemoryAreaBits.DataMemory);
        }

        private static bool WriteStringOmron(PlcOmron plc, ushort address, string value, int maxLength = 100)
        {
            var stringBytes = Encoding.ASCII.GetBytes(value ?? "");
            var bytes = new byte[maxLength];

            Array.Copy(stringBytes, bytes, Math.Min(stringBytes.Length, maxLength));

            if (bytes.Length % 2 != 0)
            {
                Array.Resize(ref bytes, bytes.Length + 1);
            }

            ushort wordCount = (ushort)(bytes.Length / 2);

            return plc.Write(address, bytes, 0, wordCount, MemoryAreaBits.DataMemory);
        }

        private static bool WriteBoolOmron(PlcOmron plc, ushort wordAddress, byte bit, bool value)
        {
            byte bitValue = (byte)(value ? 1 : 0);

            return plc.Write(wordAddress, new byte[] { bitValue }, bit, 1, MemoryAreaBits.Work);
        }
    }
}
