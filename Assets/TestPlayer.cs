using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayer : MonoBehaviour
{
    public float speed = 6f;
    public float jumpSpeed = 8f;
    public float gravity = 20f;

    private CharacterController controller;
    private Vector3 MoveDir;

    void Start()
    {
        MoveDir = Vector3.zero;
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 현재 캐릭터가 땅에 있는가?
        if (controller.isGrounded)
        {
            MoveDir = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            MoveDir = transform.TransformDirection(MoveDir);
            MoveDir *= speed;
            if (Input.GetButton("Jump"))
                MoveDir.y = jumpSpeed;

        }

        MoveDir.y -= gravity * Time.deltaTime;

        controller.Move(MoveDir * Time.deltaTime);
    }
}
