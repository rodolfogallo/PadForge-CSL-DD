using System;
using System.Linq;
using System.Text.Json;
using PadForge.Common;
using PadForge.Common.Input;
using PadForge.Engine;
using PadForge.Engine.Common;

namespace PadForge.Services
{
    public partial class InputService
    {
        internal static bool ShellManaged => System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ps5-emu-shell.engine"));
        private static bool IsShellPad(PadForge.Engine.Data.UserDevice device) =>
            device.CapType == InputDeviceType.Gamepad;

        private void AssignShellGamepads()
        {
            if (!ShellManaged || DeviceAssignments == null) return;
            var store = SettingsManager.UserDevices;
            PadForge.Engine.Data.UserDevice[] devices;
            lock (store.SyncRoot) devices = store.Items.ToArray();
            foreach (var device in devices)
            {
                if (device.ShellAutoSlot >= 0 &&
                    device.DevicePath?.StartsWith("XInput#", StringComparison.OrdinalIgnoreCase) == true &&
                    DeviceAssignments.SetDeviceSlotAssignment(device.InstanceGuid, device.ShellAutoSlot, false))
                {
                    DeviceAssignments.SetSlotEnabled(device.ShellAutoSlot, false);
                    SettingsManager.SlotCreated[device.ShellAutoSlot] = false;
                    device.ShellAutoSlot = -1;
                    device.HidHideEnabled = false;
                    _settingsService.MarkDirty();
                }
                if (!device.IsOnline || !IsShellPad(device) || device.ExcludeFromShellAutoAssign ||
                    SettingsManager.GetAssignedSlots(device.InstanceGuid).Count != 0) continue;
                int slot = Array.FindIndex(SettingsManager.SlotCreated, created => !created);
                if (slot < 0) break;
                if (Enumerable.Range(0, InputManager.MaxPads).Count(i =>
                    SettingsManager.SlotCreated[i] && _mainVm.Pads[i].OutputType == VirtualControllerType.Xbox) >= 4) break;
                _mainVm.Pads[slot].OutputType = VirtualControllerType.Xbox;
                if (DeviceAssignments.SetDeviceSlotAssignment(device.InstanceGuid, slot, true))
                {
                    device.ExcludeFromShellAutoAssign = false;
                    device.ShellAutoSlot = slot;
                    device.HidHideEnabled = true;
                    _settingsService.MarkDirty();
                }
            }
            for (int i = 0; i < InputManager.MaxPads; i++)
            {
                if (!SettingsManager.SlotCreated[i]) continue;
                bool active = devices.Any(d => d.IsOnline && SettingsManager.GetAssignedSlots(d.InstanceGuid).Contains(i));
                if (SettingsManager.SlotEnabled[i] != active) DeviceAssignments.SetSlotEnabled(i, active);
            }
            ApplyDeviceHiding();
        }

        private string ShellDevicesCommand(string verb, string arg)
        {
            if (!ShellManaged) return "error standalone";
            return _dispatcher.Invoke(() =>
            {
                var store = SettingsManager.UserDevices;
                PadForge.Engine.Data.UserDevice[] devices;
                lock (store.SyncRoot) devices = store.Items.ToArray();
                if (verb == "devices")
                    return "ok " + JsonSerializer.Serialize(new {
                        devices = devices.Where(d => IsShellPad(d) || d.CapType == InputDeviceType.Joystick ||
                            d.CapType == InputDeviceType.Driving || d.CapType == InputDeviceType.Flight)
                            .Select(d => new { id = d.InstanceGuid, name = d.ProductName, online = d.IsOnline,
                                excluded = d.ExcludeFromShellAutoAssign, gamepad = IsShellPad(d),
                                slots = SettingsManager.GetAssignedSlots(d.InstanceGuid) }),
                        slots = Enumerable.Range(0, InputManager.MaxPads).Where(i => SettingsManager.SlotCreated[i])
                            .Select(i => new { index = i, type = _mainVm.Pads[i].OutputType.ToString(),
                                online = devices.Any(d => d.IsOnline && SettingsManager.GetAssignedSlots(d.InstanceGuid).Contains(i)) })
                    });

                if (verb == "combine" || verb == "edit-combined")
                {
                    var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int slot = Array.FindIndex(SettingsManager.SlotCreated, created => !created);
                    bool editing = verb == "edit-combined";
                    if (editing)
                    {
                        if (parts.Length < 2 || !int.TryParse(parts[0], out slot) ||
                            slot < 0 || slot >= InputManager.MaxPads || !SettingsManager.SlotCreated[slot] ||
                            _mainVm.Pads[slot].OutputType != VirtualControllerType.Extended) return "error invalid-slot";
                        parts = parts.Skip(1).ToArray();
                    }
                    if (parts.Length < 1 || parts.Distinct().Count() != parts.Length) return "error invalid-devices";
                    var selected = parts.Select(id => Guid.TryParse(id, out var guid)
                        ? devices.FirstOrDefault(d => d.InstanceGuid == guid) : null).ToArray();
                    if (selected.Any(d => d == null || SettingsManager.GetAssignedSlots(d.InstanceGuid).Any(i => !editing || i != slot) ||
                        !(IsShellPad(d) || d.CapType == InputDeviceType.Joystick ||
                          d.CapType == InputDeviceType.Driving || d.CapType == InputDeviceType.Flight)))
                        return "error invalid-devices";
                    if (slot < 0) return "error no-slot";
                    _mainVm.Pads[slot].OutputType = VirtualControllerType.Extended;
                    var previous = devices.Where(d => SettingsManager.GetAssignedSlots(d.InstanceGuid).Contains(slot)).ToArray();
                    foreach (var d in selected)
                    {
                        if (!DeviceAssignments.SetDeviceSlotAssignment(d.InstanceGuid, slot, true))
                            return "error assignment-failed";
                        d.ExcludeFromShellAutoAssign = true;
                        d.HidHideEnabled = true;
                    }
                    foreach (var d in previous.Except(selected))
                    {
                        if (!DeviceAssignments.SetDeviceSlotAssignment(d.InstanceGuid, slot, false))
                            return "error assignment-failed";
                        d.HidHideEnabled = SettingsManager.GetAssignedSlots(d.InstanceGuid).Count != 0;
                    }
                    _settingsService.MarkDirty();
                    AssignShellGamepads();
                    return "ok " + slot;
                }

                if (!Guid.TryParse(arg, out var deviceId)) return "error invalid-device";
                var device = devices.FirstOrDefault(d => d.InstanceGuid == deviceId && IsShellPad(d));
                if (device == null) return "error invalid-device";
                if (verb == "exclude")
                {
                    if (device.ShellAutoSlot >= 0 && !DeviceAssignments.SetDeviceSlotAssignment(
                        deviceId, device.ShellAutoSlot, false)) return "error assignment-failed";
                    if (device.ShellAutoSlot >= 0) { DeviceAssignments.SetSlotEnabled(device.ShellAutoSlot, false); SettingsManager.SlotCreated[device.ShellAutoSlot] = false; }
                    device.ShellAutoSlot = -1;
                    device.ExcludeFromShellAutoAssign = true;
                    device.HidHideEnabled = SettingsManager.GetAssignedSlots(deviceId).Count != 0;
                }
                else device.ExcludeFromShellAutoAssign = false;
                _settingsService.MarkDirty();
                AssignShellGamepads();
                return "ok updated";
            });
        }
    }
}
