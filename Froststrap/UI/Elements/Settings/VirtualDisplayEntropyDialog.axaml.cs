// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Froststrap.UI.Elements.Settings
{
    internal partial class VirtualDisplayEntropyDialog : Base.AvaloniaWindow
    {
        private const int SamplesPerPass = 500;

        private const ulong FnvOffset = 0xcbf29ce484222325;
        private const ulong FnvPrime = 0x100000001b3;

        private readonly List<ulong> _passA = [];
        private readonly List<ulong> _passB = [];
        private bool _onPassB;

        public uint? Result { get; private set; }

        public VirtualDisplayEntropyDialog()
        {
            InitializeComponent();
            CaptureArea.PointerMoved += OnPointerMoved;
        }

        public static async Task<uint?> RequestAsync()
        {
            var dialog = new VirtualDisplayEntropyDialog();

            if (MainWindow.Instance is { } owner)
                return await dialog.ShowDialog<uint?>(owner);

            var tcs = new TaskCompletionSource<uint?>();
            dialog.Closed += (_, _) => tcs.SetResult(dialog.Result);
            dialog.Show();
            return await tcs.Task;
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var pos = e.GetPosition(this);
            var sample = Mix(
                (ulong)(long)pos.X,
                (ulong)(long)pos.Y,
                e.Timestamp
            );

            if (!_onPassB)
            {
                _passA.Add(sample);
                if (_passA.Count >= SamplesPerPass)
                {
                    _onPassB = true;
                    InstructionText.Text =
                        "Again! One more pass (pass 2 of 2)";
                    HintText.Text = "Almost there, keep moving!";
                }
            }
            else
            {
                _passB.Add(sample);
                if (_passB.Count >= SamplesPerPass)
                {
                    CaptureArea.PointerMoved -= OnPointerMoved;
                    Result = DeriveSerial(
                        [.. _passA],
                        [.. _passB]
                    );
                    Close(Result);
                    return;
                }
            }

            int done = _onPassB ? _passA.Count + _passB.Count : _passA.Count;
            Progress.Value = done / (2.0 * SamplesPerPass);
        }

        private static uint DeriveSerial(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
        {
            ulong sumA = 0;
            foreach (var v in a)
                sumA = unchecked(sumA + v);

            int index = (int)(sumA % (ulong)b.Length);

            ulong h = FnvOffset;
            for (int i = 0; i < a.Length; i++)
            {
                int j = (index + i) % b.Length;
                h ^= a[i];
                h *= FnvPrime;
                h ^= b[j];
                h *= FnvPrime;
            }

            h ^= sumA;
            h *= FnvPrime;

            uint serial = (uint)(h ^ (h >> 32));
            return serial == 0 ? 1u : serial;
        }

        private static ulong Mix(ulong x, ulong y, ulong timestamp)
        {
            ulong h = FnvOffset;
            h ^= x;
            h *= FnvPrime;
            h ^= y;
            h *= FnvPrime;
            h ^= timestamp;
            h *= FnvPrime;
            return h;
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
