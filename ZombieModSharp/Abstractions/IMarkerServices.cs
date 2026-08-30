using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZombieModSharp.Abstractions;

public interface IMarkerServices
{
    public bool CreateMarker(IGameClient client, Vector position);
    public void CleanupAll();

    public void DisableLastMarker();
}