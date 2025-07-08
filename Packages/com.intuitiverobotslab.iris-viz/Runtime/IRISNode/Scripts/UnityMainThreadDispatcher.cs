using System;
using System.Collections.Generic;

public class UnityMainThreadDispatcher : Singleton<UnityMainThreadDispatcher>
{
    private readonly Queue<Action> _actions = new Queue<Action>();
        
    public void Enqueue(Action action)
    {
        lock (_actions)
        {
            _actions.Enqueue(action);
        }
    }
    
    void Update()
    {
        lock (_actions)
        {
            while (_actions.Count > 0)
            {
                _actions.Dequeue().Invoke();
            }
        }
    }
}