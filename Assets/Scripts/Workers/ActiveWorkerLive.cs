using UnityEngine;
using UnityEngine.AI;

public class ActiveWorkerLive : MonoBehaviour
{
  public Transform target;
    public float moveThreshold = 1f;
 
    private NavMeshAgent agent;
    private Animator animator;
 
    private Vector3 lastTargetPosition;
 
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
 
        if (target != null)
            lastTargetPosition = target.position;
    }
 
    void Update()
    {
        if (target == null)
            return;
 
        float distanceMoved = Vector3.Distance(
            lastTargetPosition,
            target.position
        );
 
        if (distanceMoved >= moveThreshold)
        {
            agent.SetDestination(target.position);
            lastTargetPosition = target.position;
        }
 
        // Walking
        if (agent.hasPath &&
            agent.remainingDistance > agent.stoppingDistance)
        {
            animator.SetInteger("WorkerAction", 1);
        }
        // Idle
        else
        {
            animator.SetInteger("WorkerAction", 0);
        }
 
        // Clear path when reached
        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance)
        {
            agent.ResetPath();
            animator.SetInteger("WorkerAction", 0);
        }
    }
}
