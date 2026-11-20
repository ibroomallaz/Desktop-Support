using DSAMVVM.MVVM.ViewModel.Cards;

namespace DSAMVVM.Core.Formatters
{
    /// <summary>
    /// Formats <see cref="EntraHistoryItemViewModel"/> device metadata, ownership, and Entra group memberships for clipboard export.
    /// </summary>
    public static class EntraCardClipboardHelper
    {
        public static CardClipboardBuilder BuildSummary(EntraHistoryItemViewModel vm)
        {
            var builder = new CardClipboardBuilder();

            if (!vm.IsFound || vm.SelectedDevice == null)
            {
                builder.AddField("Entra Device Search", $"{vm.Query} (Not Found)");
                if (!string.IsNullOrWhiteSpace(vm.ErrorMessage))
                {
                    builder.AddField("Error", vm.ErrorMessage);
                }
                return builder;
            }

            AppendHeader(builder, vm);
            AppendBadges(builder, vm);
            AppendDeviceDetails(builder, vm);
            AppendGroups(builder, vm);

            return builder;
        }

        public static void AppendHeader(CardClipboardBuilder builder, EntraHistoryItemViewModel vm)
        {
            builder.AddHeader(vm.DeviceName, vm.JoinTypeDisplay);
        }

        public static void AppendBadges(CardClipboardBuilder builder, EntraHistoryItemViewModel vm)
        {
            if (vm.IsDisabled)
            {
                builder.AddBadge("Disabled");
            }
            else if (vm.IsEnabled)
            {
                builder.AddBadge("Enabled");
            }

            if (vm.SelectedDevice?.IsManaged == true)
            {
                builder.AddBadge("Managed");
            }

            if (vm.HasDivision)
            {
                builder.AddBadge($"DIV: {vm.DivisionCode}");
            }
        }

        public static void AppendDeviceDetails(CardClipboardBuilder builder, EntraHistoryItemViewModel vm)
        {
            var d = vm.SelectedDevice;
            if (d == null) return;

            builder.AddField("Object ID", d.ObjectId);
            builder.AddField("Device ID", d.DeviceId);
            if (vm.HasDivision)
            {
                builder.AddField("Division", vm.DivisionCode);
            }
            builder.AddField("Operating System", vm.OperatingSystemDisplay);

            if (vm.HasHardware)
            {
                builder.AddField("Hardware", vm.HardwareDisplay);
            }

            builder.AddField("MDM", vm.MdmDisplayName);
            builder.AddField("Join Type", vm.JoinTypeDisplay);
            builder.AddField("Last Activity", vm.LastActivityDisplay);
            builder.AddField("Registration Date", vm.RegistrationDateDisplay);
            builder.AddField("On-Prem Sync", vm.OnPremSyncDisplay);

            if (d.Owners.Count > 0)
            {
                builder.AddField("Device Owner(s)", string.Join(", ", d.Owners.Select(FormatPerson)));
            }
        }

        public static void AppendGroups(CardClipboardBuilder builder, EntraHistoryItemViewModel vm)
        {
            if (vm.AllGroups.Count == 0) return;

            var items = vm.AllGroups.Select(g =>
            {
                string scope = g.IsDirect ? "Direct" : "Transitive";
                string rule = g.IsDynamic ? "Dynamic" : "Assigned";
                string source = g.IsSyncedFromOnPrem ? "AD Synced" : "Cloud";
                return $"{g.DisplayName} [{scope} | {rule} | {source}]";
            });

            builder.AddList("Entra Groups", items);
        }

        private static string FormatPerson(MVVM.Model.Entra.EntraDeviceOwnerInfo person)
        {
            string identity = !string.IsNullOrWhiteSpace(person.UserPrincipalName)
                ? person.UserPrincipalName
                : person.Mail ?? person.ObjectId;
            return !string.IsNullOrWhiteSpace(person.DisplayName)
                ? $"{person.DisplayName} ({identity})"
                : identity;
        }
    }
}
