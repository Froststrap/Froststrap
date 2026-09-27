using System;
using System.Collections.Generic;
using System.Text;

namespace Froststrap.UI.Elements.Overlay
{
    internal enum OverlayToggleAction
    {
        Hide,
        Present,
        Ignore
    }

    internal static class OverlayToggle
    {
        public static OverlayToggleAction Decide(bool visible, bool inFront, bool gameMinimised)
        {
            if (visible && (inFront || gameMinimised))
                return OverlayToggleAction.Hide;

            if (gameMinimised)
                return OverlayToggleAction.Ignore;

            return OverlayToggleAction.Present;
        }
    }
}
