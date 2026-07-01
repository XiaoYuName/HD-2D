using System;
using UnityEngine;
using XFramework;

public class ClawMachineController : GameBase
{
    private Rigidbody2D hock;

    private bool isMove;
    private bool isDropping;
    private bool isRising;
    private Vector2 moveSpeed;


    private void Start()
    {
        hock = Get<Rigidbody2D>("HockController");
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isDropping = true;
        }

        if (!isDropping && !isRising)
        {
            moveSpeed.x = Input.GetAxisRaw("Horizontal");
            isMove = true;
        }
        else
        {
            moveSpeed.x = 0;
            isMove = false;
        }

        if (!isMove && Input.GetKeyDown(KeyCode.Space))
        {
            moveSpeed.y = -1;
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            moveSpeed.y = 1;
        }



    }

    private void FixedUpdate()
    {
        if(hock == null)return;
        if (isMove)
        {
            Vector2 nextPos = hock.position + moveSpeed * Time.fixedDeltaTime;
            hock.MovePosition(nextPos);
        }

        if (isDropping)
        {
            Vector2 nextPos = hock.position + moveSpeed * Time.fixedDeltaTime;
            hock.MovePosition(nextPos);
        }

        if (isRising)
        {
            Vector2 nextPos = hock.position + moveSpeed * Time.fixedDeltaTime;
            hock.MovePosition(nextPos);
        }
        
    }
}
