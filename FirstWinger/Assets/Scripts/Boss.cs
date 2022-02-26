using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Boss : Enemy
{
    const float FireTransformRotationStart = -30.0f;
    const float FireTransformRotationInterval = 15.0f;

    [SyncVar]
    bool needBattleMove = false;

    [SerializeField]
    float BattleMoveMax;

    Vector3 BattleMoveStartPos;

    int FireRemainCountPerOnetime;

    [SyncVar]
    float BattleMoveLength;

    [SyncVar]
    [SerializeField]
    Vector3 CurrentFireTransformRotation;

    protected override void SetBattleState()
    {
        base.SetBattleState();
        BattleMoveStartPos = transform.position;
        FireRemainCountPerOnetime = FireRemainCount;

        //ȸ���� �ʱ�ȭ
        CurrentFireTransformRotation.z = FireTransformRotationStart;
        Quaternion quat = Quaternion.identity;
        quat.eulerAngles = CurrentFireTransformRotation;
        FireTransform.localRotation = quat;
    }

    protected override void UpdateBattle()
    {
        if (needBattleMove)
        {
            UpdateBattleMove();
        }
        else
        {
            if (Time.time - LastActionUpdateTime > 1.0f)
            {
                if (FireRemainCountPerOnetime > 0)
                {
                    Fire();
                    RotateFireTransform();
                    FireRemainCountPerOnetime--;
                }
                else
                {
                    SetBattleMove();
                }

                LastActionUpdateTime = Time.time;
            }
        }
    }

    void SetBattleMove()
    {
        if (!isServer)
            return;

        // ������ �������� �̵��� �����ϱ� ���� �κ�
        float halfPingpongHeight = 0.0f;
        float rand = Random.value;
        if (rand < 0.5f)
            halfPingpongHeight = BattleMoveMax * 0.5f;
        else
            halfPingpongHeight = -BattleMoveMax * 0.5f;
        // ������ �Ÿ��� �̵��ϱ� ���� �κ�
        float newBattleMoveLength = Random.Range(BattleMoveMax, BattleMoveMax * 3.0f);  //  BattleMoveMax �� 1��~ 3�� ������ �Ÿ��� �̵�

        RpcSetBattleMove(halfPingpongHeight, newBattleMoveLength);        // Host �÷��̾��ΰ�� RPC�� ������
    }

    [ClientRpc]
    public void RpcSetBattleMove(float halfPingpongHeight, float newBattleMoveLength)
    {
        needBattleMove = true;
        TargetPosition = BattleMoveStartPos;
        TargetPosition.y += halfPingpongHeight;

        CurrentSpeed = 0.0f;           // ��������� 0���� �ӵ� ����
        MoveStartTime = Time.time;

        BattleMoveLength = newBattleMoveLength;

        base.SetDirtyBit(1);
    }

    void UpdateBattleMove()
    {
        UpdateSpeed();

        Vector3 oldPosition = transform.position;
        float distance = Vector3.Distance(TargetPosition, transform.position);
        if (distance == 0)
        {
            if (isServer)
                RpcChangeBattleMoveTarget();        // Host �÷��̾��ΰ�� RPC�� ������
        }

        transform.position = Vector3.SmoothDamp(transform.position, TargetPosition, ref CurrentVelocity, distance / CurrentSpeed, MaxSpeed * 0.2f);

        BattleMoveLength -= Vector3.Distance(oldPosition, transform.position);
        if (BattleMoveLength <= 0)
            SetBattleFire();

    }

    [ClientRpc]
    public void RpcChangeBattleMoveTarget()
    {
        if (TargetPosition.y > BattleMoveStartPos.y)
            TargetPosition.y = BattleMoveStartPos.y - BattleMoveMax * 0.5f;
        else
            TargetPosition.y = BattleMoveStartPos.y + BattleMoveMax * 0.5f;

        base.SetDirtyBit(1);
    }

    void SetBattleFire()
    {
        if (isServer)
            RpcSetBattleFire();        // Host �÷��̾��ΰ�� RPC�� ������
    }

    [ClientRpc]
    public void RpcSetBattleFire()
    {
        needBattleMove = false;
        MoveStartTime = Time.time;
        FireRemainCountPerOnetime = FireRemainCount;
        // ȸ������ �ʱ�ȭ
        CurrentFireTransformRotation.z = FireTransformRotationStart;
        Quaternion quat = Quaternion.identity;
        quat.eulerAngles = CurrentFireTransformRotation;
        FireTransform.localRotation = quat;

        base.SetDirtyBit(1);
    }

    void RotateFireTransform()
    {
        if (isServer)
            RpcRotateFireTransform();        // Host �÷��̾��ΰ�� RPC�� ������
    }

    [ClientRpc]
    public void RpcRotateFireTransform()
    {
        CurrentFireTransformRotation.z += FireTransformRotationInterval;
        Quaternion quat = Quaternion.identity;
        quat.eulerAngles = CurrentFireTransformRotation;
        FireTransform.localRotation = quat;

        base.SetDirtyBit(1);
    }
}