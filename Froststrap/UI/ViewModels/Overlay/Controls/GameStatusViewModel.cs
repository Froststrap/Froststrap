using Froststrap.Enums;
using Froststrap.RobloxInterfaces;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class GameStatusViewModel : VisibilityViewModel
    {
        public override string Title => Strings.Menu_Overlay_GamePrivacy_Title;

        public override string Hint => Strings.Menu_Overlay_GamePrivacy_Hint;

        protected override string SettingName => "game visibility";

        protected override IReadOnlyList<PrivacyLevel> Available => State?.JoinOptions ?? [];

        protected override PrivacyLevel? Current => State?.Join;

        protected override bool IsAllowed(PrivacyLevel value) => PrivacySettings.GameVisibilityAllowed(value, State?.Online);

        protected override async Task<string> ApplyAsync(PrivacyLevel value)
        {
            const string LOG_IDENT = "GameStatusViewModel::ApplyAsync";

            await PrivacySettings.SetGameVisibilityAsync(value, State?.Online);

            App.Logger.Info($"{LOG_IDENT}: Game visibility is now {value}");

            return Strings.Menu_Overlay_Privacy_Saved;
        }
    }
}