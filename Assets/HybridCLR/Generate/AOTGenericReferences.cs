using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

    // {{ AOT assemblies
    public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
    {
        "DOTween.dll",
        "QFramework.CoreKit.dll",
        "QFramework.dll",
        "ResKit.dll",
        "System.Core.dll",
        "TeaGame.App.dll",
        "UIKit.dll",
        "UnityEngine.CoreModule.dll",
        "mscorlib.dll",
    };
    // }}

    // {{ constraint implement type
    // }} 

    // {{ AOT generic types
    // QFramework.Architecture.<>c<object>
    // QFramework.Architecture<object>
    // QFramework.BindableProperty.<>c<UnityEngine.Vector2>
    // QFramework.BindableProperty.<>c<float>
    // QFramework.BindableProperty.<>c<int>
    // QFramework.BindableProperty.<>c<object>
    // QFramework.BindableProperty.<>c__DisplayClass17_0<UnityEngine.Vector2>
    // QFramework.BindableProperty.<>c__DisplayClass17_0<float>
    // QFramework.BindableProperty.<>c__DisplayClass17_0<int>
    // QFramework.BindableProperty.<>c__DisplayClass17_0<object>
    // QFramework.BindableProperty<UnityEngine.Vector2>
    // QFramework.BindableProperty<float>
    // QFramework.BindableProperty<int>
    // QFramework.BindableProperty<object>
    // QFramework.CustomObjectFactory<object>
    // QFramework.DefaultObjectFactory<object>
    // QFramework.EasyEvent.<>c<UnityEngine.Vector2>
    // QFramework.EasyEvent.<>c<float>
    // QFramework.EasyEvent.<>c<int>
    // QFramework.EasyEvent.<>c<object>
    // QFramework.EasyEvent.<>c__DisplayClass1_0<UnityEngine.Vector2>
    // QFramework.EasyEvent.<>c__DisplayClass1_0<float>
    // QFramework.EasyEvent.<>c__DisplayClass1_0<int>
    // QFramework.EasyEvent.<>c__DisplayClass1_0<object>
    // QFramework.EasyEvent.<>c__DisplayClass4_0<UnityEngine.Vector2>
    // QFramework.EasyEvent.<>c__DisplayClass4_0<float>
    // QFramework.EasyEvent.<>c__DisplayClass4_0<int>
    // QFramework.EasyEvent.<>c__DisplayClass4_0<object>
    // QFramework.EasyEvent<UnityEngine.Vector2>
    // QFramework.EasyEvent<float>
    // QFramework.EasyEvent<int>
    // QFramework.EasyEvent<object>
    // QFramework.IObjectFactory<object>
    // QFramework.MonoSingletonProperty<object>
    // QFramework.Pool<object>
    // QFramework.SafeObjectPool<object>
    // QFramework.UIKitTable<object>
    // System.Action<TeaGame.BattleEvent_ControlTakeOverRequested>
    // System.Action<TeaGame.BattleEvent_EnemyHit>
    // System.Action<TeaGame.BattleEvent_EnemyKilled>
    // System.Action<TeaGame.BattleEvent_LevelStateChanged>
    // System.Action<TeaGame.BattleEvent_PauseResumeRequested>
    // System.Action<TeaGame.BattleEvent_PauseToggleRequested>
    // System.Action<TeaGame.BattleEvent_PlayerDamaged>
    // System.Action<TeaGame.BattleEvent_PlayerDied>
    // System.Action<TeaGame.BattleEvent_RoleDowned>
    // System.Action<TeaGame.BattleEvent_RoleHealthChanged>
    // System.Action<TeaGame.BattleEvent_RoleSwitched>
    // System.Action<TeaGame.BattleEvent_RollTriggered>
    // System.Action<TeaGame.BattleEvent_StageTimedOut>
    // System.Action<TeaGame.BattleEvent_Stage_Cleared>
    // System.Action<TeaGame.BattleEvent_Stage_WaveStarted>
    // System.Action<UnityEngine.Vector2>
    // System.Action<byte>
    // System.Action<float>
    // System.Action<int,byte>
    // System.Action<int>
    // System.Action<object,object>
    // System.Action<object>
    // System.ByReference<ushort>
    // System.Collections.Generic.ArraySortHelper<int>
    // System.Collections.Generic.ArraySortHelper<object>
    // System.Collections.Generic.Comparer<int>
    // System.Collections.Generic.Comparer<object>
    // System.Collections.Generic.Dictionary.Enumerator<int,object>
    // System.Collections.Generic.Dictionary.Enumerator<object,byte>
    // System.Collections.Generic.Dictionary.Enumerator<object,object>
    // System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
    // System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,byte>
    // System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
    // System.Collections.Generic.Dictionary.KeyCollection<int,object>
    // System.Collections.Generic.Dictionary.KeyCollection<object,byte>
    // System.Collections.Generic.Dictionary.KeyCollection<object,object>
    // System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
    // System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,byte>
    // System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
    // System.Collections.Generic.Dictionary.ValueCollection<int,object>
    // System.Collections.Generic.Dictionary.ValueCollection<object,byte>
    // System.Collections.Generic.Dictionary.ValueCollection<object,object>
    // System.Collections.Generic.Dictionary<int,object>
    // System.Collections.Generic.Dictionary<object,byte>
    // System.Collections.Generic.Dictionary<object,object>
    // System.Collections.Generic.EqualityComparer<byte>
    // System.Collections.Generic.EqualityComparer<int>
    // System.Collections.Generic.EqualityComparer<object>
    // System.Collections.Generic.HashSet.Enumerator<object>
    // System.Collections.Generic.HashSet<object>
    // System.Collections.Generic.HashSetEqualityComparer<object>
    // System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
    // System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,byte>>
    // System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
    // System.Collections.Generic.ICollection<int>
    // System.Collections.Generic.ICollection<object>
    // System.Collections.Generic.IComparer<int>
    // System.Collections.Generic.IComparer<object>
    // System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
    // System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,byte>>
    // System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
    // System.Collections.Generic.IEnumerable<int>
    // System.Collections.Generic.IEnumerable<object>
    // System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
    // System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,byte>>
    // System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
    // System.Collections.Generic.IEnumerator<int>
    // System.Collections.Generic.IEnumerator<object>
    // System.Collections.Generic.IEqualityComparer<int>
    // System.Collections.Generic.IEqualityComparer<object>
    // System.Collections.Generic.IList<int>
    // System.Collections.Generic.IList<object>
    // System.Collections.Generic.IReadOnlyCollection<object>
    // System.Collections.Generic.IReadOnlyList<object>
    // System.Collections.Generic.KeyValuePair<int,object>
    // System.Collections.Generic.KeyValuePair<object,byte>
    // System.Collections.Generic.KeyValuePair<object,object>
    // System.Collections.Generic.List.Enumerator<int>
    // System.Collections.Generic.List.Enumerator<object>
    // System.Collections.Generic.List<int>
    // System.Collections.Generic.List<object>
    // System.Collections.Generic.ObjectComparer<int>
    // System.Collections.Generic.ObjectComparer<object>
    // System.Collections.Generic.ObjectEqualityComparer<byte>
    // System.Collections.Generic.ObjectEqualityComparer<int>
    // System.Collections.Generic.ObjectEqualityComparer<object>
    // System.Collections.Generic.Stack.Enumerator<object>
    // System.Collections.Generic.Stack<object>
    // System.Collections.ObjectModel.ReadOnlyCollection<int>
    // System.Collections.ObjectModel.ReadOnlyCollection<object>
    // System.Comparison<int>
    // System.Comparison<object>
    // System.Func<System.Threading.Tasks.VoidTaskResult>
    // System.Func<UnityEngine.Vector2,UnityEngine.Vector2,byte>
    // System.Func<float,float,byte>
    // System.Func<int,int,byte>
    // System.Func<object,System.Threading.Tasks.VoidTaskResult>
    // System.Func<object,byte>
    // System.Func<object,object,byte>
    // System.Func<object,object,object>
    // System.Func<object>
    // System.Nullable<UnityEngine.Quaternion>
    // System.Nullable<int>
    // System.Predicate<int>
    // System.Predicate<object>
    // System.ReadOnlySpan<ushort>
    // System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>
    // System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.Threading.Tasks.VoidTaskResult>
    // System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.Threading.Tasks.VoidTaskResult>
    // System.Runtime.CompilerServices.TaskAwaiter<System.Threading.Tasks.VoidTaskResult>
    // System.Span<ushort>
    // System.Threading.Tasks.ContinuationTaskFromResultTask<System.Threading.Tasks.VoidTaskResult>
    // System.Threading.Tasks.Task<System.Threading.Tasks.VoidTaskResult>
    // System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.Threading.Tasks.VoidTaskResult>
    // System.Threading.Tasks.TaskFactory<System.Threading.Tasks.VoidTaskResult>
    // }}

    public void RefMethods()
    {
        // object DG.Tweening.TweenSettingsExtensions.SetEase<object>(object,DG.Tweening.Ease)
        // System.Void QFramework.Architecture<object>.RegisterModel<object>(object)
        // System.Void QFramework.Architecture<object>.RegisterSystem<object>(object)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_ControlTakeOverRequested>(System.Action<TeaGame.BattleEvent_ControlTakeOverRequested>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_EnemyHit>(System.Action<TeaGame.BattleEvent_EnemyHit>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_EnemyKilled>(System.Action<TeaGame.BattleEvent_EnemyKilled>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_LevelStateChanged>(System.Action<TeaGame.BattleEvent_LevelStateChanged>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_PauseResumeRequested>(System.Action<TeaGame.BattleEvent_PauseResumeRequested>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_PauseToggleRequested>(System.Action<TeaGame.BattleEvent_PauseToggleRequested>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_PlayerDamaged>(System.Action<TeaGame.BattleEvent_PlayerDamaged>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_PlayerDied>(System.Action<TeaGame.BattleEvent_PlayerDied>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_RoleDowned>(System.Action<TeaGame.BattleEvent_RoleDowned>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_RoleHealthChanged>(System.Action<TeaGame.BattleEvent_RoleHealthChanged>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_RoleSwitched>(System.Action<TeaGame.BattleEvent_RoleSwitched>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_RollTriggered>(System.Action<TeaGame.BattleEvent_RollTriggered>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_StageTimedOut>(System.Action<TeaGame.BattleEvent_StageTimedOut>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_Stage_Cleared>(System.Action<TeaGame.BattleEvent_Stage_Cleared>)
        // QFramework.IUnRegister QFramework.IArchitecture.RegisterEvent<TeaGame.BattleEvent_Stage_WaveStarted>(System.Action<TeaGame.BattleEvent_Stage_WaveStarted>)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_ControlTakeOverRequested>(TeaGame.BattleEvent_ControlTakeOverRequested)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_EnemyHit>(TeaGame.BattleEvent_EnemyHit)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_EnemyKilled>(TeaGame.BattleEvent_EnemyKilled)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_LevelStateChanged>(TeaGame.BattleEvent_LevelStateChanged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_LockEnemyChanged>(TeaGame.BattleEvent_LockEnemyChanged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_PauseResumeRequested>(TeaGame.BattleEvent_PauseResumeRequested)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_PauseToggleRequested>(TeaGame.BattleEvent_PauseToggleRequested)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_PlayerDamaged>(TeaGame.BattleEvent_PlayerDamaged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_PlayerDied>(TeaGame.BattleEvent_PlayerDied)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_ReloadCompleted>(TeaGame.BattleEvent_ReloadCompleted)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_RoleDowned>(TeaGame.BattleEvent_RoleDowned)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_RoleHealthChanged>(TeaGame.BattleEvent_RoleHealthChanged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_RoleSwitched>(TeaGame.BattleEvent_RoleSwitched)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_RollTriggered>(TeaGame.BattleEvent_RollTriggered)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_SkillCooldownChanged>(TeaGame.BattleEvent_SkillCooldownChanged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_SkillEnergyChanged>(TeaGame.BattleEvent_SkillEnergyChanged)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_StageTimedOut>(TeaGame.BattleEvent_StageTimedOut)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_Stage_Cleared>(TeaGame.BattleEvent_Stage_Cleared)
        // System.Void QFramework.IArchitecture.SendEvent<TeaGame.BattleEvent_Stage_WaveStarted>(TeaGame.BattleEvent_Stage_WaveStarted)
        // System.Void QFramework.IOCContainer.Register<object>(object)
        // object QFramework.IResLoaderExtensions.LoadSync<object>(QFramework.IResLoader,string,string)
        // System.Void QFramework.UIKit.ClosePanel<object>()
        // object QFramework.UIKit.GetPanel<object>()
        // object QFramework.UIKit.OpenPanel<object>(QFramework.UILevel,QFramework.IUIData,string,string)
        // object[] System.Array.Empty<object>()
        // System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1>(System.Runtime.CompilerServices.TaskAwaiter&,TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1&)
        // System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1>(System.Runtime.CompilerServices.TaskAwaiter&,TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1&)
        // System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1>(TeaGame.StaticExpansion.<AudioVolumeLerpAsync>d__1&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,TeaGame.BattleWeaponMode_AssaultPreciseFrame.<ExecuteBurstAsync>d__1>(System.Runtime.CompilerServices.TaskAwaiter&,TeaGame.BattleWeaponMode_AssaultPreciseFrame.<ExecuteBurstAsync>d__1&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,TeaGame.Gacha_MainControl.<Button_Back>d__40>(System.Runtime.CompilerServices.TaskAwaiter&,TeaGame.Gacha_MainControl.<Button_Back>d__40&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,TeaGame.Gacha_MainControl.<Start>d__24>(System.Runtime.CompilerServices.TaskAwaiter&,TeaGame.Gacha_MainControl.<Start>d__24&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<TeaGame.BattleWeaponMode_AssaultPreciseFrame.<ExecuteBurstAsync>d__1>(TeaGame.BattleWeaponMode_AssaultPreciseFrame.<ExecuteBurstAsync>d__1&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<TeaGame.Gacha_MainControl.<Button_Back>d__40>(TeaGame.Gacha_MainControl.<Button_Back>d__40&)
        // System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<TeaGame.Gacha_MainControl.<Start>d__24>(TeaGame.Gacha_MainControl.<Start>d__24&)
        // object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
        // System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
        // bool TeaGame.UiPanelFlow.CloseAlive<object>()
        // System.Void TeaGame.UiPanelFlow.DiscardHalfOpened<object>()
        // bool TeaGame.UiPanelFlow.IsAlive<object>()
        // object TeaGame.UiPanelFlow.TryOpen<object>(QFramework.UILevel,QFramework.IUIData,string,string)
        // object UnityEngine.Component.GetComponent<object>()
        // object UnityEngine.Component.GetComponentInChildren<object>()
        // object UnityEngine.Component.GetComponentInParent<object>()
        // object[] UnityEngine.Component.GetComponentsInChildren<object>(bool)
        // bool UnityEngine.Component.TryGetComponent<object>(object&)
        // object UnityEngine.GameObject.AddComponent<object>()
        // object UnityEngine.GameObject.GetComponent<object>()
        // object UnityEngine.GameObject.GetComponentInChildren<object>()
        // object UnityEngine.GameObject.GetComponentInChildren<object>(bool)
        // object[] UnityEngine.GameObject.GetComponentsInChildren<object>(bool)
        // bool UnityEngine.GameObject.TryGetComponent<object>(object&)
        // object UnityEngine.Object.FindObjectOfType<object>()
        // object[] UnityEngine.Object.FindObjectsOfType<object>()
        // object UnityEngine.Object.Instantiate<object>(object)
        // object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform)
        // object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
        // object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Vector3,UnityEngine.Quaternion)
        // object[] UnityEngine.Resources.ConvertObjects<object>(UnityEngine.Object[])
        // object UnityEngine.Resources.GetBuiltinResource<object>(string)
        // object UnityEngine.ScriptableObject.CreateInstance<object>()
    }
}
