using DSAMVVM.MVVM.ViewModel.Cards;

namespace DSAMVVM.Core.Formatters
{
    public static class ComputerCardClipboardHelper
    {
        public static CardClipboardBuilder BuildSummary(ComputerHistoryItemViewModel vm)
        {
            var builder = new CardClipboardBuilder();

            if (!vm.IsFound)
            {
                builder.AddField("Computer Search", $"{vm.Query} (Not Found)");
                if (!string.IsNullOrWhiteSpace(vm.ErrorMessage))
                {
                    builder.AddField("Error", vm.ErrorMessage);
                }
                return builder;
            }

            AppendHeader(builder, vm);
            AppendBadges(builder, vm);
            AppendSystemDetails(builder, vm);

            return builder;
        }

        public static void AppendHeader(CardClipboardBuilder builder, ComputerHistoryItemViewModel vm)
        {
            builder.AddHeader(vm.ComputerName);
        }

        public static void AppendBadges(CardClipboardBuilder builder, ComputerHistoryItemViewModel vm)
        {
            if (vm.IsDisabled)
            {
                builder.AddBadge("Disabled");
            }
            else if (vm.IsEnabled)
            {
                builder.AddBadge("Active");
            }
        }

        public static void AppendSystemDetails(CardClipboardBuilder builder, ComputerHistoryItemViewModel vm)
        {
            builder.AddField("Operating System", vm.OperatingSystem);
            builder.AddField("Hybrid Group", vm.IsHybridGroupMember ? "In UA-MEMHybridDevices" : "Not in UA-MEMHybridDevices");

            if (vm.HasLastLogon)
            {
                builder.AddField("Last Logon", vm.LastLogonDate);
            }

            if (vm.HasOUs)
            {
                builder.AddField("Organizational Unit", vm.CleanOuPath);
            }

            if (vm.HasDescription)
            {
                builder.AddField("Description", vm.Description);
            }
        }
    }
}
