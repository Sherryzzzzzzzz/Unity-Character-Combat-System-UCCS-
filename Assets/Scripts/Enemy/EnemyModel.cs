using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEngine;

public class EnemyModel : MonoBehaviour,IStateOwner,Parryable.IBehaviorController
{
    private CharacterController cc;
    public float speed;
    private StateMachine stateMachine;
    private GameObject _player;
    public Vector2 moveDir;
    public float angle;
    public ExpandableAnimationSet AnimationSet;
    public bool isRunning;
    public float rotateSpeed = 5f;
    public bool isHitting = false;
    public BehaviorTree tree;
    

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        _player = GameObject.FindGameObjectWithTag("Player");
        tree = GetComponent<BehaviorTree>();
        
    }

    private void Update()
    {
        isHitting = GetComponent<HurtBoxManager>().isHitting;
        if (isHitting)
        {
            tree.enabled = false;
            return;
        }
        else
            tree.enabled = true;
        if (!isRunning)
        {
            Vector3 toTarget = _player.transform.position - transform.position;
            toTarget.y = 0; // 保持水平方向旋转

            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
            }   
        }
        angle = Vector3.SignedAngle(new Vector3(moveDir.x, 0, moveDir.y), transform.forward, Vector3.up);
    }
    
    public void InterruptAndDisableBehavior()
    {
        if (tree != null)
        {
            tree.DisableBehavior();
        }
    }

    public void ResumeBehavior()
    {
        if (tree != null)
        {
            tree.EnableBehavior();
        }
    }
}
