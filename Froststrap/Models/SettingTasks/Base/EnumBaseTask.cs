// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Froststrap.Models.SettingTasks.Base
{
    internal abstract class EnumBaseTask<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> : BaseTask where T : struct, Enum
    {
        private T _originalState = default!;

        private T _newState = default!;

        public virtual T OriginalState
        {
            get => _originalState;

            set
            {
                _originalState = value;
                _newState = value;
            }
        }

        public virtual T NewState
        {
            get => _newState;

            set
            {
                _newState = value;

                if (Changed)
                    App.PendingSettingTasks[Name] = this;
                else
                    App.PendingSettingTasks.Remove(Name);
            }
        }

        public override bool Changed => !_newState.Equals(OriginalState);

        public IEnumerable<T> Selections { get; private set; }
            = Enum.GetValues<T>().OrderBy(x =>
            {
                var attribute = typeof(T).GetField(x.ToString())?.GetCustomAttribute<EnumSortAttribute>();

                return attribute?.Order ?? 0;
            });

        public EnumBaseTask(string prefix, string name) : base(prefix, name) { }
    }
}
