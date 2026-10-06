// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Hugr
{
    /// <summary>
    /// Every error code Hugr logs, <c>HUGR-DOMAIN-NNN</c>: the domain names the feature, the
    /// number the step that failed, 000 being the unexpected failure caught at the feature's
    /// boundary. One code, one place in the code: a code is used once, and a retired number is
    /// never given to another failure.
    /// </summary>
    internal static class ErrorCodes
    {
        internal const string ClaimTimedOut = "HUGR-CLAIM-001";

        internal const string ClockStopped = "HUGR-CLOCK-000";

        internal const string CompassStopped = "HUGR-COMPASS-000";

        internal const string CraftUnexpected = "HUGR-CRAFT-000";
        internal const string CraftPullIncomplete = "HUGR-CRAFT-001";
        internal const string CraftFetchFailed = "HUGR-CRAFT-002";
        internal const string CraftPieceFetchFailed = "HUGR-CRAFT-003";

        internal const string FilterStopped = "HUGR-FILTER-000";
        internal const string FilterButtonTemplateMissing = "HUGR-FILTER-001";
        internal const string FilterLabelMissing = "HUGR-FILTER-002";

        internal const string PatchUnexpected = "HUGR-PATCH-000";
        internal const string PatchTargetMissing = "HUGR-PATCH-001";

        internal const string RecipeUnexpected = "HUGR-RECIPE-000";
        internal const string RecipeSelectionMissing = "HUGR-RECIPE-001";
        internal const string RecipeTitleMissing = "HUGR-RECIPE-002";
        internal const string RecipeIconMissing = "HUGR-RECIPE-003";
        internal const string RecipeAmountMissing = "HUGR-RECIPE-004";
        internal const string RecipeOfSelectionMissing = "HUGR-RECIPE-005";
        internal const string RecipeTrackerStopped = "HUGR-RECIPE-006";
        internal const string RecipeNameMissing = "HUGR-RECIPE-007";

        internal const string RepairUnexpected = "HUGR-REPAIR-000";
        internal const string RepairRoutineMissing = "HUGR-REPAIR-001";
        internal const string RepairNotConverging = "HUGR-REPAIR-002";
        internal const string AutoRepairUnexpected = "HUGR-REPAIR-003";

        internal const string SearchStopped = "HUGR-SEARCH-000";
        internal const string SearchTemplateMissing = "HUGR-SEARCH-001";
        internal const string SearchFieldMissing = "HUGR-SEARCH-002";
        internal const string RecipeSearchUnexpected = "HUGR-SEARCH-003";
        internal const string RecipeSearchRefreshFailed = "HUGR-SEARCH-004";

        internal const string ServerUnexpected = "HUGR-SERVER-000";

        internal const string SmeltUnexpected = "HUGR-SMELT-000";
        internal const string SmeltInteractMissing = "HUGR-SMELT-001";
        internal const string SmeltResumeFailed = "HUGR-SMELT-002";

        internal const string SortButtonsUnexpected = "HUGR-SORT-000";
        internal const string SortChangeNotificationMissing = "HUGR-SORT-001";
        internal const string SortContainerUnreachable = "HUGR-SORT-002";
        internal const string SortButtonTemplateMissing = "HUGR-SORT-003";
        internal const string SortLabelMissing = "HUGR-SORT-004";
        internal const string SortRefused = "HUGR-SORT-005";
        internal const string SortBadgeMissing = "HUGR-SORT-006";

        internal const string StackUnexpected = "HUGR-STACK-000";

        internal const string UiUnexpected = "HUGR-UI-000";
        internal const string UiTabHandlerMissing = "HUGR-UI-001";
        internal const string UiNoTab = "HUGR-UI-002";
        internal const string UiTabButtonMissing = "HUGR-UI-005";
        internal const string UiTabLabelMissing = "HUGR-UI-006";
        internal const string UiRowToggleMissing = "HUGR-UI-007";
        internal const string UiRowCaptionMissing = "HUGR-UI-008";
        internal const string UiRowTemplateMissing = "HUGR-UI-009";
        internal const string UiFieldUnreachable = "HUGR-UI-010";
        internal const string UiRowIsPage = "HUGR-UI-011";
        internal const string UiKeyHintsUnreachable = "HUGR-UI-012";
        internal const string UiSaveFailed = "HUGR-UI-013";
        internal const string UiLoadFailed = "HUGR-UI-014";
        internal const string UiVersionCaptionMissing = "HUGR-UI-015";
        internal const string UiRowLost = "HUGR-UI-016";
        internal const string UiScrollTemplateMissing = "HUGR-UI-017";
    }
}
