// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

global using System;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using System.Linq;
global using System.Net;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

global using Froststrap.Enums;
global using Froststrap.Enums.AppStoragePresets;
global using Froststrap.Enums.BootstrapperDialogs;
global using Froststrap.Enums.FlagPresets;
global using Froststrap.Enums.GBSPresets;
global using Froststrap.Enums.Messagebox;
global using Froststrap.Exceptions;
global using Froststrap.Models;
global using Froststrap.Models.APIs;
global using Froststrap.Models.APIs.Config;
global using Froststrap.Models.APIs.GitHub;
global using Froststrap.Models.APIs.RoValra;
global using Froststrap.Models.APIs.Roblox;
global using Froststrap.Models.APIs.WebView2;
global using Froststrap.Models.Attributes;
global using Froststrap.Models.BloxstrapRPC;
global using Froststrap.Models.FroststrapStudioRPC;
global using Froststrap.Models.Manifest;
global using Froststrap.Models.Persistable;
global using Froststrap.Models.RobloxApi;
global using Froststrap.Utility;
global using Froststrap.Localisation;
