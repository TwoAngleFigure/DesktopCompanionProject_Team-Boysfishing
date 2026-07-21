using DesktopCompanion.Save;
using System;
using UnityEngine;


public class FishingSettingSystem : ISaveable
{

    public string SaveId => throw new NotImplementedException();

    public Type StateType => throw new NotImplementedException();

    public object CaptureState()
    {
        throw new NotImplementedException();
    }

    public void RestoreState(object state)
    {
        throw new NotImplementedException();
    }
}
