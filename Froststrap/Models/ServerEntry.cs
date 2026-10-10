﻿using System.Windows.Input;
using Froststrap.Models.Overlay;

namespace Froststrap.Models
{
    internal class ServerEntry : GameServer
    {
        public int Number { get; set; }
        public int? DataCenterId { get; set; }
        public ICommand? JoinCommand { get; set; }
        public ICommand? ShareCommand { get; set; }
    }
}