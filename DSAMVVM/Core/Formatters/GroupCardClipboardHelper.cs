using DSAMVVM.MVVM.ViewModel;
using DSAMVVM.MVVM.ViewModel.Cards;

namespace DSAMVVM.Core.Formatters
{
    public static class GroupCardClipboardHelper
    {
        public static CardClipboardBuilder BuildSummary(GroupHistoryItemViewModel vm)
        {
            var builder = new CardClipboardBuilder();

            if (!vm.IsFound)
            {
                builder.AddField(vm.ModeBadgeText, $"{vm.Query} (Not Found)");
                if (!string.IsNullOrWhiteSpace(vm.ErrorMessage))
                {
                    builder.AddField("Error", vm.ErrorMessage);
                }
                return builder;
            }

            builder.AddHeader(vm.Query);
            builder.AddBadge(vm.ModeBadgeText);

            switch (vm.Mode)
            {
                case GroupViewModel.GroupSearchMode.UserMim:
                    AppendUserMimDetails(builder, vm);
                    break;

                case GroupViewModel.GroupSearchMode.GroupMembers:
                    AppendGroupMembersDetails(builder, vm);
                    break;

                case GroupViewModel.GroupSearchMode.Department:
                    AppendDepartmentSupportDetails(builder, vm);
                    break;

                case GroupViewModel.GroupSearchMode.Division:
                    AppendDivisionSupportDetails(builder, vm);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return builder;
        }

        private static void AppendUserMimDetails(CardClipboardBuilder builder, GroupHistoryItemViewModel vm)
        {
            if (vm.IsUserDisabled)
            {
                builder.AddBadge("Disabled");
            }

            if (vm.HasMimWarning)
            {
                builder.AddWarning("User is missing 'UA-MIM-Wrkst-AllDivUsers' Group");
            }

            if (vm.HasMimGroups)
            {
                builder.AddList("MIM GROUPS", vm.AllMimGroups);
            }
        }

        private static void AppendGroupMembersDetails(CardClipboardBuilder builder, GroupHistoryItemViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.NormalizedGroupName))
            {
                builder.AddField("Group", vm.NormalizedGroupName);
            }

            builder.AddField("Member Count", vm.MemberCount.ToString());

            if (vm.HasMembers)
            {
                builder.AddList("MEMBERS", vm.AllGroupMembers);
            }
        }

        private static void AppendDepartmentSupportDetails(CardClipboardBuilder builder, GroupHistoryItemViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.DeptNumber))
            {
                builder.AddField("Dept Number", vm.DeptNumber);
            }

            if (vm.HasDeptTeam)
            {
                builder.AddField("Support Team", vm.DeptTeamName);
            }

            if (vm.HasDeptManager)
            {
                string mgr = !string.IsNullOrWhiteSpace(vm.DeptManagerNetId)
                    ? $"{vm.DeptManagerName} ({vm.DeptManagerNetId})"
                    : vm.DeptManagerName!;
                builder.AddField("Manager", mgr);
            }

            if (vm.HasDeptPhone)
            {
                builder.AddField("Support Phone", vm.DeptSupportPhone);
            }

            if (!string.IsNullOrWhiteSpace(vm.DeptNotes))
            {
                builder.AddField("Dept Notes", vm.DeptNotes, isItalic: true);
            }
        }

        private static void AppendDivisionSupportDetails(CardClipboardBuilder builder, GroupHistoryItemViewModel vm)
        {
            if (vm.DivTeams.Count <= 0) return;
            var teamNames = vm.DivTeams.Select(t => t.TeamName).Where(n => !string.IsNullOrWhiteSpace(n));
            builder.AddList("SUPPORT TEAMS", teamNames);
        }
    }
}
