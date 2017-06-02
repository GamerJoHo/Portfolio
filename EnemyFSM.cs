using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFSM : MonoBehaviour {

    public enum EnemyState { Idle, Warning, Alert, Subdue, Return, Watch, Patrol }
    public EnemyState enemyState;

    public float minWait, maxWait;
    public float moveSpeed;

    public float alertDistance;
    public float catchDistance;

    [Range(2, 10)]
    public int alertChecker; // How many times should we check for the player whilst in alert stage?

    public bool canPatrol;

    public Transform returnPosition;
    public Transform[] patrolPoints;
    Transform patrolDestination;

    Coroutine co;
    GameObject activePlayer;

    Vector3 warningDir;
    Vector3 lastKnownPos;

    SpriteRenderer sr;
    Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        enemyState = EnemyState.Idle;
        co = StartCoroutine(FSM());
    }

    void Update()
    {
        // Input test for debug purposes
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ChangeState(EnemyState.Warning);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ChangeState(EnemyState.Alert);
        }

        // The below regions are the triggers that cause the enemy to change states
        #region subdue

        if (activePlayer != null && enemyState != EnemyState.Subdue)
        {
            if (Vector2.Distance(activePlayer.transform.position, transform.position) < catchDistance)
            {
                ChangeState(EnemyState.Subdue);
            }
        }

        #endregion subdue

        #region warning
        if (enemyState == EnemyState.Warning)
        {
            sr.color = Color.yellow;
            if (Vector2.Distance(transform.position, lastKnownPos) > 1)
            {
                // Enemy is more than 1 unit away from the last known position - move towards
                Debug.Log("Inside here");
                rb.velocity = new Vector3(moveSpeed * warningDir.x, rb.velocity.y, 0f);

                transform.localScale = new Vector3(warningDir.x, 1f, 1f);

                // Send a raycast from the enemy forward to check for player
                RaycastHit2D hit = Physics2D.Raycast(transform.position, new Vector2(transform.localScale.x, 0), catchDistance);
                if (Physics2D.Raycast(transform.position, new Vector2(transform.localScale.x, 0), catchDistance))
                {
                    if (hit.collider.name.ToString().Substring(0, 6) == "Player")
                    {
                        ChangeState(EnemyState.Alert);
                    }
                }
            }
            else
            {
                GameObject activePlayer = LevelManager.activePlayer;
                if (Vector2.Distance(activePlayer.transform.position, transform.position) < alertDistance)
                {
                    ChangeState(EnemyState.Alert);
                }
                else
                {
                    ChangeState(EnemyState.Idle);
                }
            }
        }
        #endregion warning

        #region alert

        if (enemyState == EnemyState.Alert)
        {
            Vector2 dir = (activePlayer.transform.position - transform.position).normalized;
            rb.velocity = new Vector3(moveSpeed * dir.x, rb.velocity.y, 0f);
            transform.localScale = new Vector3(dir.x, 1f, 1f);

            if (Vector2.Distance(activePlayer.transform.position, transform.position) > alertDistance)
            {
                // Player is far enough away, check periodically to see if still far away
                StartCoroutine(CheckAlert(alertChecker));
                ChangeState(EnemyState.Idle);
            }
        }

        #endregion alert

        #region return
        if (enemyState == EnemyState.Return)
        {
            Vector3 dir = (returnPosition.position - transform.position).normalized;

            if (Vector2.Distance(transform.position, returnPosition.position) > 1)
            {
                rb.velocity = new Vector3(dir.x * moveSpeed, rb.velocity.y, 0f);
                transform.localScale = new Vector3(dir.x, 1f, 1f);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        #endregion return

        #region patrol

        Vector2 patrolDir = (patrolDestination.position - transform.position).normalized;
        if (Vector2.Distance(patrolDestination.position, transform.position) < 1)
        {
            ChangeState(EnemyState.Idle);
        }
        else
        {
            rb.velocity = new Vector3(patrolDir.x * moveSpeed, rb.velocity.y, 0f);
        }
        #endregion patrol
    }

    IEnumerator FSM()
    {
        switch (enemyState)
        {
            case EnemyState.Idle:
                sr.color = Color.blue;
                float timeToWait = Random.Range(minWait, maxWait);
                while (enemyState == EnemyState.Idle)
                {
                    yield return new WaitForSeconds(timeToWait);
                }
                ChangeState(EnemyState.Idle);
                break;


            case EnemyState.Warning:
                sr.color = Color.yellow;
                // Calculate global variables for use in update
                activePlayer = LevelManager.activePlayer;
                lastKnownPos = activePlayer.transform.position;
                warningDir = (lastKnownPos - transform.position).normalized;

                yield return null;
                break;


            case EnemyState.Alert:
                sr.color = Color.red;
                activePlayer = LevelManager.activePlayer;
                break;


            case EnemyState.Subdue:
                sr.color = Color.black;
                activePlayer = LevelManager.activePlayer;
                
                if (activePlayer.GetComponent<Rigidbody2D>())
                {
                    activePlayer.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                }
                else
                {
                    Debug.LogError("Trying to subdue something that is not the player!");
                }

                break;


            case EnemyState.Return:
                sr.color = Color.green;
                Debug.Log("Returning");
                break;


            case EnemyState.Watch:
                break;

            case EnemyState.Patrol:
                sr.color = Color.cyan;
                GetPatrolPosition();
                break;
        }
    }

    IEnumerator CheckAlert(int alertChecks)
    {
        for (int i = 0; i < alertChecks; i++)
        {
            Debug.Log("Checked for " + i + " seconds");
            yield return new WaitForSeconds(1);
            if (Vector2.Distance(activePlayer.transform.position, transform.position) < alertDistance)
            {
                ChangeState(EnemyState.Alert);
                break;
            }

            if (i == (alertChecks - 1))
            {
                ChangeState(EnemyState.Return);
            }
        }
    }

    void ChangeState(EnemyState state)
    {
        if (state == EnemyState.Patrol && !canPatrol)
        {
            Debug.LogWarning("Trying to assign patrol to an NPC who cannot patrol - resetting to watch");
            ChangeState(EnemyState.Watch);
        }

        enemyState = state;

        if (co != null)
        {
            StopCoroutine(co);
        }

        co = StartCoroutine(FSM());
    }

    void GetPatrolPosition()
    {
        int pos = Random.Range(0, patrolPoints.Length);
        patrolDestination = patrolPoints[pos];
    }

}
