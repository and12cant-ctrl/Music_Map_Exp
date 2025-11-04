using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveReader : MonoBehaviour
{
    private bool canMove = true;
    private GameManager gm;

    private void Start()
    {
        gm = GetComponentInParent<GameManager>();
    }
    public void OnMove(InputValue value)
    {
        if( !canMove) return;

        Vector2 moveInput = value.Get<Vector2>();
        Vector2Int dir = Vector2Int.zero;
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            int sx = (moveInput.x > 0f) ? 1 : -1;
            dir = new Vector2Int(sx, 0);
        }
        else
        {
            int sy = (moveInput.y > 0f) ? 1 : -1;
            dir = new Vector2Int(0, sy);
        }
        StartCoroutine(WaitTime(dir));
    }

    private IEnumerator WaitTime(Vector2Int move)
    {
        canMove = false;
        gm.PlayerCell += move;
        Debug.Log(gm.PlayerCell);
        yield return new WaitForSeconds(0.5f);
        canMove = true;
    }
}
