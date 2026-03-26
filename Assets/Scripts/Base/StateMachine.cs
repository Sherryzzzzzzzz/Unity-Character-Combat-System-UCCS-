using System.Collections;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using System;

public interface IStateOwner { }


public class StateMachine//统一的状态机
{
     private StateBase currentState;//当前状态

     private IStateOwner owner;

     private Dictionary<Type,StateBase> states = new Dictionary<Type, StateBase>();

#if UNITY_EDITOR
     public struct StateTransitionRecord
     {
          public Type FromState;
          public Type ToState;
          public float Timestamp;
     }

     public Type PreviousStateType { get; private set; }
     private const int MaxTransitionHistory = 20;
     public List<StateTransitionRecord> TransitionHistory { get; } = new List<StateTransitionRecord>();
     public event Action OnStateChanged;
     public IEnumerable<Type> RegisteredStateTypes => states.Keys;
#endif

     public StateMachine(IStateOwner owner)
     {
          this.owner = owner;
     }

     public Type CurrentStateType => currentState?.GetType();

#if UNITY_EDITOR
     private void RecordTransition(Type fromState, Type toState)
     {
          PreviousStateType = fromState;
          TransitionHistory.Add(new StateTransitionRecord
          {
               FromState = fromState,
               ToState = toState,
               Timestamp = UnityEngine.Time.time
          });
          if (TransitionHistory.Count > MaxTransitionHistory)
               TransitionHistory.RemoveAt(0);
          OnStateChanged?.Invoke();
     }
#endif

     public void EnterState<T>() where T: StateBase,new()//进入继承statebase的状态
     {
          if (CurrentStateType == typeof(T))
          {
               // 如果请求的状态类型和当前状态类型相同，则直接返回
               return;
          }
          if(currentState != null && currentState.GetType() == typeof(T)) return;
#if UNITY_EDITOR
          Type previousType = currentState?.GetType();
#endif
          if (currentState != null)
               currentState.Exit();
          currentState = LoadState<T>();
          currentState.Enter();
#if UNITY_EDITOR
          RecordTransition(previousType, typeof(T));
#endif
     }

     public void EnterState<T>(object parameter = null) where T: StateBase,new()//进入继承statebase的状态
     {
          if(currentState != null && currentState.GetType() == typeof(T)) return;
#if UNITY_EDITOR
          Type previousType = currentState?.GetType();
#endif
          if (currentState != null)
               currentState.Exit();
          currentState = LoadState<T>();
          currentState.Enter(parameter);
#if UNITY_EDITOR
          RecordTransition(previousType, typeof(T));
#endif
     }

     private StateBase LoadState<T>() where T : StateBase, new()
     {
          Type stateType = typeof(T);
          if (!states.TryGetValue(stateType, out StateBase state))
          {
               state = new T();
               state.Init(owner);
               states.Add(stateType, state);
          }
          return state;
     }

     public void Stop()
     {
          if(currentState != null)
               currentState.Exit();
          foreach (var state in states.Values)
          {
               state.Destroy();
          }
     }
}
