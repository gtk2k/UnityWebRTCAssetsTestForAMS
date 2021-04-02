using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityWebRTCForAMSTest
{
    public enum ClientType
    {
        Publisher,
        Player
    }

    public enum LogLevel
    {
        Log,
        Warning,
        Error
    }

    public enum AssetType
    {
        AMS,
        Unity
    }

    public enum VideoResolution
    {
        _2160p,
        _1440p,
        _1080p,
        _720p,
        _480p,
        _360p,
        _240p
    }

    public enum SendDataInterval
    {
        _100ms,
        _1s,
        _5s,
        _10s
    }

}
