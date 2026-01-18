using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Diagnostics;
using System.Media;
using Froststrap.Resources;
using Froststrap.UI.Elements.Controls;

namespace Froststrap.UI.Elements.Dialogs
{
    public partial class FluentMessageBox : Base.AvaloniaWindow
    {
        public MessageBoxResult Result = MessageBoxResult.None;

        public FluentMessageBox(string message, MessageBoxImage image, MessageBoxButton buttons)
        {
            InitializeComponent();

            string? iconFilename = null;

            switch (image)
            {
                case MessageBoxImage.Error:
                    iconFilename = "Error";
                    break;

                case MessageBoxImage.Question:
                    iconFilename = "Question";
                    break;

                case MessageBoxImage.Warning:
                    iconFilename = "Warning";
                    break;

                case MessageBoxImage.Information:
                    iconFilename = "Information";
                    break;
            }

            if (iconFilename is null)
                IconImage.IsVisible = false;
            else
                IconImage.Source = new Bitmap($"avares://Froststrap/Resources/MessageBox/{iconFilename}.png");

            Title = App.ProjectName;

            MessageMarkdownTextBlock.MarkdownText = message;

            ButtonOne.IsVisible = false;
            ButtonTwo.IsVisible = false;
            ButtonThree.IsVisible = false;

            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    SetButton(ButtonOne, MessageBoxResult.Yes);
                    SetButton(ButtonTwo, MessageBoxResult.No);
                    break;

                case MessageBoxButton.YesNoCancel:
                    SetButton(ButtonOne, MessageBoxResult.Yes);
                    SetButton(ButtonTwo, MessageBoxResult.No);
                    SetButton(ButtonThree, MessageBoxResult.Cancel);
                    break;

                case MessageBoxButton.OKCancel:
                    SetButton(ButtonOne, MessageBoxResult.OK);
                    SetButton(ButtonTwo, MessageBoxResult.Cancel);
                    break;

                case MessageBoxButton.OK:
                default:
                    SetButton(ButtonOne, MessageBoxResult.OK);
                    break;
            }

            if (ButtonThree.IsVisible)
                Width = 356;
            else if (ButtonTwo.IsVisible)
                Width = 245;

            double textWidth = 180;

            if (image != MessageBoxImage.None)
                textWidth += 50;

            textWidth += message.Length * 0.6;

            if (textWidth > MaxWidth)
                Width = MaxWidth;
            else if (textWidth > Width)
                Width = textWidth;

            Loaded += (s, e) =>
            {
                // avalonia dosent have this so we will skip it for now
            };
        }

        private static string GetTextForResult(MessageBoxResult result)
        {
            switch (result)
            {
                case MessageBoxResult.OK:
                    return Strings.Common_OK;
                case MessageBoxResult.Cancel:
                    return Strings.Common_Cancel;
                case MessageBoxResult.Yes:
                    return Strings.Common_Yes;
                case MessageBoxResult.No:
                    return Strings.Common_No;
                default:
                    Debug.Assert(false);
                    return result.ToString();
            }
        }

        public void SetButton(Button button, MessageBoxResult result)
        {
            button.IsVisible = true;
            button.Content = GetTextForResult(result);
            button.Click += (_, _) =>
            {
                Result = result;
                Close();
            };
        }
    }
}