// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.VisualTree;
using Froststrap.UI.Elements.Controls;

namespace Froststrap.UI.Utility
{
    /// <summary>
    /// Utility for building search indexes from UI elements.
    /// Scans majoirty of elements in search page (OptionControls, CardExpanders, CardActions) etc.
    /// Should be fairly competent at doing its job.
    /// </summary>
    internal sealed partial class SearchIndexBuilder
    {
        private readonly Dictionary<string, List<SearchBarItem>> _pageIndexCache = [];


        public List<SearchBarItem> ScanRenderedPageForElements(Control pageView, string pageTag)
        {
            List<SearchBarItem> newItems = [];

            try
            {
                if (_pageIndexCache.ContainsKey(pageTag))
                {
                    return newItems;
                }

                ScanForCardExpanders(pageView, pageTag, "", newItems);
                ScanForOptionControls(pageView, pageTag, "", newItems);
                ScanForCardActions(pageView, pageTag, "", newItems);
                ScanForSquareCards(pageView, pageTag, "", newItems);

                if (newItems.Count > 0)
                {
                    _pageIndexCache[pageTag] = newItems;
                }

                return newItems;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Error scanning rendered page {pageTag}: {ex.Message}");
                return newItems;
            }
        }

        private static void ScanForCardExpanders(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var cardExpanders = pageView.GetVisualDescendants()
                .OfType<CardExpander>()
                .Where(c => !IsGeneratedItem(c));

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

        private static void ScanForOptionControls(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var optionControls = pageView.GetVisualDescendants()
                .OfType<OptionControl>()
                .Where(c => !IsGeneratedItem(c));

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

        private static void ScanForCardActions(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var cardActions = pageView.GetVisualDescendants()
                .OfType<CardAction>()
                .Where(c => !IsGeneratedItem(c));

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

        private static void ScanForSquareCards(Control pageView, string pageTag, string pageTitle, List<SearchBarItem> searchIndex)
        {
            var squareCards = pageView.GetVisualDescendants()
                .OfType<SquareCard>()
                .Where(c => !IsGeneratedItem(c));

            foreach (var card in squareCards)
            {
                var header = card.Header as string;
                if (!string.IsNullOrWhiteSpace(header))
                {
                    searchIndex.Add(new SearchBarItem
                    {
                        DisplayName = header,
                        Tag = NormalizeTag(header),
                        PageTag = pageTag,
                        PageTitle = pageTitle,
                        Category = "Card",
                        Description = card.Description as string
                    });
                }
            }
        }

        private static bool IsGeneratedItem(Control control) =>
            control.GetVisualAncestors().OfType<ItemsControl>().Any(items => items.ItemsSource is not null);

        private static CardExpander? FindParentCardExpander(Control control)
        {
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
            return Regex.Replace(text.ToUpperInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        }
    }
}
