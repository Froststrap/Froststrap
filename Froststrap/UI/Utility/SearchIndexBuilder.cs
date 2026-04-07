using Avalonia.Controls;
using Avalonia.VisualTree;
using Froststrap.UI.Elements.Controls;

namespace Froststrap.UI.Utility
{
    /// <summary>
    /// Utility for building search indexes from UI elements.
    /// Scans majoirty of elements in search page (OptionControls, CardExpanders, CardActions, TextBlocks) etc.
    /// Should be fairly competent at doing its job.
    /// </summary>
    public class SearchIndexBuilder
    {
        private readonly Dictionary<string, List<SearchBarItem>> _pageIndexCache = new();
        private List<SearchBarItem>? _navigationItemsCache;

        public List<SearchBarItem> BuildIndex(List<(string PageTag, string PageTitle, object PageViewModel)> pages)
        {
            var searchIndex = new List<SearchBarItem>();

            searchIndex.AddRange(GetNavigationItems());

            return searchIndex;
        }

        private List<SearchBarItem> GetNavigationItems()
        {
            if (_navigationItemsCache != null)
            {
                return _navigationItemsCache;
            }

            var navigationItems = new[]
            {
                new { Display = Resources.Strings.Menu_Integrations_Title, Tag = "integrations", Action = (Action?)null },
                new { Display = Resources.Strings.Menu_Behaviour_Title, Tag = "behaviour", Action = (Action?)null },
                new { Display = "Preset Mods", Tag = "mods", Action = (Action?)null },
                new { Display = Resources.Strings.Menu_FastFlags_Title, Tag = "fastflags", Action = (Action?)null },
                new { Display = Resources.Strings.Menu_Appearance_Title, Tag = "appearance", Action = (Action?)null },
                new { Display = Resources.Strings.Menu_RegionSelector_Title, Tag = "regionselector", Action = (Action?)null },
                new { Display = Resources.Strings.Menu_GlobalSettings_Title, Tag = "globalsettings", Action = (Action?)null },
                new { Display = Resources.Strings.Common_Shortcuts, Tag = "shortcuts", Action = (Action?)null },
                new { Display = Resources.Strings.Common_Settings, Tag = "channels", Action = (Action?)null }
            };

            _navigationItemsCache = new List<SearchBarItem>();
            foreach (var item in navigationItems)
            {
                _navigationItemsCache.Add(new SearchBarItem
                {
                    DisplayName = item.Display,
                    Tag = item.Tag,
                    NavigateAction = null,
                    PageTag = item.Tag,
                    Category = "Navigation"
                });
            }

            return _navigationItemsCache;
        }

        public void InvalidatePageCache(string pageTag)
        {
            _pageIndexCache.Remove(pageTag);
        }
        public void ClearCache()
        {
            _pageIndexCache.Clear();
            _navigationItemsCache = null;
        }

        public List<SearchBarItem> ScanRenderedPageForElements(Control pageView, string pageTag)
        {
            var newItems = new List<SearchBarItem>();

            try
            {
                if (_pageIndexCache.ContainsKey(pageTag))
                {
                    return newItems;
                }

                ScanForCardExpanders(pageView, pageTag, "", newItems);
                ScanForOptionControls(pageView, pageTag, "", newItems);
                ScanForCardActions(pageView, pageTag, "", newItems);
                ScanForTextLabels(pageView, pageTag, "", newItems);

                if (newItems.Count > 0)
                {
                    _pageIndexCache[pageTag] = newItems;
                }

                return newItems;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("SearchIndexBuilder::ScanRenderedPageForElements", 
                    $"Error scanning rendered page {pageTag}: {ex.Message}");
                return newItems;
            }
        }

        private Control? FindPageView(object pageViewModel)
        {
            // This would normally be called from a page instance
            // For now, we return null and this will be called with actual view instances from code-behind
            return null;
        }

        private void ScanForCardExpanders(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var cardExpanders = pageView.GetVisualDescendants()
                .OfType<CardExpander>()
                .ToList();

            foreach (var expander in cardExpanders)
            {
                var header = expander.Header as string ?? (expander.Header as TextBlock)?.Text;
                if (!string.IsNullOrWhiteSpace(header))
                {
                    searchIndex.Add(new SearchBarItem
                    {
                        DisplayName = header,
                        Tag = NormalizeTag(header),
                        PageTag = pageTag,
                        PageTitle = pageTitle,
                        Category = "Section",
                        Description = expander.Description as string
                    });
                }
            }
        }

        private void ScanForOptionControls(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var optionControls = pageView.GetVisualDescendants()
                .OfType<OptionControl>()
                .ToList();

            foreach (var option in optionControls)
            {
                var title = option.Header;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    var parentExpander = FindParentCardExpander(option);
                    var parentSectionName = parentExpander?.Header as string;

                    searchIndex.Add(new SearchBarItem
                    {
                        DisplayName = title,
                        Tag = NormalizeTag(title),
                        PageTag = pageTag,
                        PageTitle = pageTitle,
                        Category = "Setting",
                        Description = option.Description,
                        ParentSectionName = parentSectionName
                    });
                }
            }
        }

        private void ScanForCardActions(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var cardActions = pageView.GetVisualDescendants()
                .OfType<CardAction>()
                .ToList();

            foreach (var action in cardActions)
            {
                var content = action.Content as string ?? (action.Content as TextBlock)?.Text;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    searchIndex.Add(new SearchBarItem
                    {
                        DisplayName = content,
                        Tag = NormalizeTag(content),
                        PageTag = pageTag,
                        PageTitle = pageTitle,
                        Category = "Action"
                    });
                }
            }
        }

        private void ScanForTextLabels(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var textBlocks = pageView.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(tb => 
                {
                    var text = tb.Text;
                    return !string.IsNullOrWhiteSpace(text) && 
                           text.Length > 3 && 
                           text.Length < 100 &&
                           !text.Contains("\n") &&
                           tb.FontWeight == Avalonia.Media.FontWeight.Bold;
                })
                .DistinctBy(tb => tb.Text)
                .ToList();

            foreach (var textBlock in textBlocks)
            {
                var text = textBlock.Text;
                if (!searchIndex.Any(item => item.DisplayName.Equals(text, StringComparison.OrdinalIgnoreCase)))
                {
                    searchIndex.Add(new SearchBarItem
                    {
                        DisplayName = text,
                        Tag = NormalizeTag(text),
                        PageTag = pageTag,
                        PageTitle = pageTitle,
                        Category = "Label"
                    });
                }
            }
        }

        private static CardExpander? FindParentCardExpander(Control control)
        {
            // Walk up the visual tree to find a parent CardExpander
            var parent = control.Parent;
            while (parent != null)
            {
                if (parent is CardExpander cardExpander)
                    return cardExpander;

                parent = (parent as Control)?.Parent;
            }
            return null;
        }

        private static string NormalizeTag(string text)
        {
            return Regex.Replace(text.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
        }
    }
}
