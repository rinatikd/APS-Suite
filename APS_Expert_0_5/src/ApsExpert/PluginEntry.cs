using Autodesk.AutoCAD.Runtime;
using ApsExpert.Commands;

[assembly: ExtensionApplication(typeof(ApsExpert.PluginEntry))]
[assembly: CommandClass(typeof(ApsExpertCommandSet))]

namespace ApsExpert;

public sealed class PluginEntry : IExtensionApplication
{
    public void Initialize()
    {
        Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
            "APS Expert 0.1 loaded. Commands: APS_SCAN, APS_AUDIT, APS_ISSUES, APS_MARK, APS_FIX, APS_EXPORT");
    }

    public void Terminate() { }
}
