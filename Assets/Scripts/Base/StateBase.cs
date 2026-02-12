using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class StateBase
{
    public abstract void Enter();

    public abstract void Enter(object parameter = null);
    
    public abstract void Exit();
    
    public abstract void Update();
    
    public abstract void Init(IStateOwner owner);
    
    public abstract void Destroy();
    
    
}
