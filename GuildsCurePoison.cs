using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;

namespace FlynsarmyGuildsCurePoisonMod
{
    public class GuildsCurePoison : MonoBehaviour
    {
        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            GameObject go = new GameObject(initParams.ModTitle);
            go.AddComponent<GuildsCurePoison>();

            ModManager.Instance.GetMod(initParams.ModTitle).IsReady = true;

            Debug.Log("GuildsCurePoison: Init");
        }

        void Awake()
        {
            UIWindowFactory.RegisterCustomUIWindow(UIWindowType.GuildServicePopup, typeof(FlynsarmyGuildServicePopupWindow));
            Debug.Log("GuildsCurePoison: GuildServicePopup registered windows");
        }
    }
}
