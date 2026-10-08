using System;
using System.Collections.Generic;
using DSAMVVM.MVVM.ViewModel.Cards;

namespace DSAMVVM.Core.Formatters
{
    public static class UserCardClipboardHelper
    {
        public static CardClipboardBuilder BuildSummary(UserHistoryItemViewModel vm)
        {
            var builder = new CardClipboardBuilder();

            if (!vm.IsFound)
            {
                builder.AddField("User Search", $"{vm.Query} (Not Found)");
                if (!string.IsNullOrWhiteSpace(vm.ErrorMessage))
                {
                    builder.AddField("Error", vm.ErrorMessage);
                }
                return builder;
            }

            AppendHeader(builder, vm);
            AppendBadges(builder, vm);
            AppendWarning(builder, vm);
            AppendDepartmentAndRouting(builder, vm);
            AppendLicensing(builder, vm);
            AppendMimGroups(builder, vm);

            return builder;
        }

        public static void AppendHeader(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            string displayName = vm.DisplayName?.Trim() ?? string.Empty;
            string netId = vm.NetId?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(netId) && displayName.Contains(netId, StringComparison.OrdinalIgnoreCase))
            {
                builder.AddHeader(displayName);
            }
            else if (!string.IsNullOrWhiteSpace(netId))
            {
                builder.AddHeader(displayName, netId);
            }
            else
            {
                builder.AddHeader(displayName);
            }
        }

        public static void AppendBadges(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            if (vm.IsLocked)
            {
                builder.AddBadge("Locked Out");
            }
            if (vm.IsDisabled)
            {
                builder.AddBadge("Disabled");
            }
            if (vm.IsActive)
            {
                builder.AddBadge("Active");
            }
            if (vm.HasDivision)
            {
                builder.AddBadge($"DIV: {vm.DivisionCode}", isBold: true);
            }
            if (!string.IsNullOrWhiteSpace(vm.Affiliation))
            {
                builder.AddBadge(vm.Affiliation);
            }
        }

        public static void AppendWarning(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            if (vm.HasMimWarning)
            {
                builder.AddWarning("User is missing 'UA-MIM-Wrkst-AllDivUsers' Group");
            }
        }

        public static void AppendDepartmentAndRouting(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            builder.AddField("Department", vm.DepartmentDisplay);

            if (vm.HasDivision)
            {
                builder.AddField("Division", vm.DivisionDisplay, isBoldValue: true);
            }

            if (vm.HasSupportTeam)
            {
                builder.AddField("Support Team", vm.SupportTeamName);
            }

            if (vm.ShowTeamDetails && (vm.HasManager || vm.HasSupportPhone))
            {
                if (vm.HasManager)
                {
                    string mgrDisplay = !string.IsNullOrWhiteSpace(vm.ManagerNetID)
                        ? $"{vm.ManagerName} ({vm.ManagerNetID})"
                        : vm.ManagerName!;
                    builder.AddField("Manager", mgrDisplay);
                }
                if (vm.HasSupportPhone)
                {
                    builder.AddField("Support Phone", vm.SupportPhone);
                }
            }

            if (vm.HasNotes)
            {
                builder.AddField("Dept Notes", vm.DepartmentNotes, isItalic: true);
            }

            if (vm.HasFileRepo)
            {
                builder.AddField("File Repo", vm.FileRepoPath);
            }
        }

        public static void AppendLicensing(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            builder.AddField("Microsoft 365", vm.LicenseSummary);

            if (vm.HasAdobeChecked)
            {
                string proStatus = vm.HasAcrobatPro ? "Assigned" : "None";
                string ccStatus = vm.HasCreativeCloud ? "Assigned" : "None";
                builder.AddField("Adobe Licenses", $"Acrobat Pro: {proStatus}  |  Creative Cloud: {ccStatus}");
            }

            if (vm.ShowRawLicense && !string.IsNullOrWhiteSpace(vm.RawLicense))
            {
                builder.AddCodeBlock("RAW LICENSE ATTRIBUTES", vm.RawLicense);
            }
        }

        public static void AppendMimGroups(CardClipboardBuilder builder, UserHistoryItemViewModel vm)
        {
            if (vm.ShowMimGroups && vm.MimGroups.Count > 0)
            {
                builder.AddList("MIM GROUPS", vm.MimGroups);
            }
        }
    }
}
