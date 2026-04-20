using CMS.server.Services;

namespace CMS.Server.Services;

/// <summary>
/// Handles Setting-page logic: password writes (delegated to PlcService)
/// and PLC signal reads. Thin wrapper — business logic lives in PlcService.
/// </summary>
public class SettingService(PlcService plcService)
{
    public object ChangeDepartmentPasswords(Dictionary<string, int> passwords)
    {
        plcService.ChangePassword(passwords);
        return new { success = true, updated = passwords.Keys };
    }

    public object ReadSubPlcSignals(int machineId)
        => plcService.ReadSubPlcSignals(machineId);

    public Dictionary<string, object?> ReadAllPlcSignals()
    {
        var result = new Dictionary<string, object?>();
        // Run in parallel — same pattern as the old bulk endpoint
        Parallel.For(1, 27, machineId =>
        {
            try
            {
                var data = plcService.ReadSubPlcSignals(machineId);
                lock (result) result[machineId.ToString()] = data;
            }
            catch
            {
                lock (result) result[machineId.ToString()] = null;
            }
        });
        return result;
    }
}