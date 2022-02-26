using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Boss : Enemy
{
    const float FireTransformRotationStart = -30.0f;
    const float FireTransformRotationInterval = 15.0f;
    const float ActionUpdateInterval = 1.0f;

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

    protected override int BulletIndex
    {
        get
        {
            return BulletManager.BossBulletIndex;
        }
    }

    [SerializeField]
    Transform[] MissileFireTransforms;

    Player[] players;

    Player[] Players
    {
        get
        {
            if (players == null)
                players = GameObject.FindObjectsOfType<Player>();
            return players;
        }
    }

    bool SpecialAttack = false;

    [SerializeField]
    float MissileSpeed = 1;

    protected override void SetBattleState()
    {
        base.SetBattleState();
        BattleMoveStartPos = transform.position;
        FireRemainCountPerOnetime = FireRemainCount;

        //회전값 초기화
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
            if (Time.time - LastActionUpdateTime > ActionUpdateInterval)
            {
                if (FireRemainCountPerOnetime > 0)
                {
                    if (SpecialAttack)
                        FireChase();
                    else
                    {
                        Fire();
                        RotateFireTransform();
                    }

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

        // 랜덤한 방향으로 이동을 시작하기 위한 부분
        float halfPingpongHeight = 0.0f;
        float rand = Random.value;
        if (rand < 0.5f)
            halfPingpongHeight = BattleMoveMax * 0.5f;
        else
            halfPingpongHeight = -BattleMoveMax * 0.5f;
        // 랜덤한 거리를 이동하기 위한 부분
        float newBattleMoveLength = Random.Range(BattleMoveMax, BattleMoveMax * 3.0f);  //  BattleMoveMax 의 1배~ 3배 사이의 거리를 이동

        RpcSetBattleMove(halfPingpongHeight, newBattleMoveLength);        // Host 플레이어인경우 RPC로 보낸다
    }

    [ClientRpc]
    public void RpcSetBattleMove(float halfPingpongHeight, float newBattleMoveLength)
    {
        needBattleMove = true;
        TargetPosition = BattleMoveStartPos;
        TargetPosition.y += halfPingpongHeight;

        CurrentSpeed = 0.0f;           // 사라질때는 0부터 속도 증가
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
                RpcChangeBattleMoveTarget();        // Host 플레이어인경우 RPC로 보낸다
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
            RpcSetBattleFire();        // Host 플레이어인경우 RPC로 보낸다
    }

    [ClientRpc]
    public void RpcSetBattleFire()
    {
        needBattleMove = false;
        MoveStartTime = Time.time;
        FireRemainCountPerOnetime = FireRemainCount;
        // 회전값을 초기화
        CurrentFireTransformRotation.z = FireTransformRotationStart;
        Quaternion quat = Quaternion.identity;
        quat.eulerAngles = CurrentFireTransformRotation;
        FireTransform.localRotation = quat;
        SpecialAttack = !SpecialAttack;

        base.SetDirtyBit(1);
    }

    void RotateFireTransform()
    {
        if (isServer)
            RpcRotateFireTransform();        // Host 플레이어인경우 RPC로 보낸다
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

    public void FireChase()
    {
        List<Player> alivePlayer = new List<Player>();
        for(int i = 0; i < Players.Length; i++)
        {
            if(!Players[i].IsDead)
            {
                alivePlayer.Add(Players[i]);
            }
        }

        int index = Random.Range(0, alivePlayer.Count);
        int targetInstanceID = alivePlayer[index].ActorInstanceID;

        GuidedMissile missile = SystemManager.Instance.GetCurrentSceneMain<InGameSceneMain>().BulletManager.Generate(BulletManager.GuidedMissileIndex) as GuidedMissile;
        if (missile)
        {
            Transform missileFireTransform = MissileFireTransforms[MissileFireTransforms.Length - FireRemainCountPerOnetime];
            missile.FireChase(targetInstanceID, actorInstanceID, missileFireTransform.position, missileFireTransform.right, MissileSpeed, Damage);
        }
    }
}