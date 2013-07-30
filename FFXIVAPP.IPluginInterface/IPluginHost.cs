﻿// FFXIVAPP.IPluginInterface
// IPluginHost.cs
//  
// Created by Ryan Wilson.
// Copyright © 2007-2013 Ryan Wilson - All Rights Reserved

#region Usings

using System.Collections.Generic;

#endregion

namespace FFXIVAPP.IPluginInterface
{
    public interface IPluginHost
    {
        void Commands(string pluginName, IEnumerable<string> commands);
        void PopupMessage(string pluginName, out bool displayed, object content);
        void GetConstants(string pluginName);
        void GetPlayerInfo(string pluginName);
        void GetMapInfo(string pluginName);
        void GetMonsterInfo(string pluginName);
    }
}