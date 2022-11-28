using UnityEngine;

namespace Platinum.Settings
{
    public enum StateScreen
    {
        Visibly,
        Unvisibly,
    }
    public class ScreenLoad : StateMachineBehaviour
    {

        public StateScreen ScreenVisibly;
        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (ScreenVisibly == StateScreen.Unvisibly)
            {
                FindObjectOfType<LoadingScreenController>().EndScreenVisibility(ScreenVisibly);
            }
        }
        override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (ScreenVisibly == StateScreen.Visibly)
            {
                FindObjectOfType<LoadingScreenController>().EndScreenVisibility(ScreenVisibly);
            }
        }
    }
}
