# MR 桌面塔防《Ghost Hunter》— Technical Spike（Unity 專案）

## 0. 專案摘要（One‑liner）
在真實桌面上自動放置高塔，玩家以右手扳機射線槍擊殺桌面生成的 Ghost，避免 Ghost 碰塔扣血直到 Game Over。

---

## 1. 專案定位與交付範圍（Scope）
本 Repo 以 **Technical Spike** 為交付目標：以「可重現、可驗收」的方式證明下列能力在 Meta Quest MR 裝置上可穩定運行（不追求完整美術、關卡與完整 UI/UX）。

- MR 場景理解：取得房間與水平面（桌面/地板）資訊（MRUK）
- 桌面放置：高塔放在玩家前方桌面，且不因初始化漂移而跑遠
- 射線射擊：右手扳機射擊命中穩定，能造成傷害與擊殺
- 塔防規則：怪物觸塔扣血；塔 HP=0 會進入 Game Over 並收斂（停用生成與射擊、清場、顯示 UI）

---

## 2. 可重現環境（Source of Truth）
以下版本以專案檔案為準：

- Unity：`6000.2.4f1`（`ProjectSettings/ProjectVersion.txt`）
- Meta XR SDK：`com.meta.xr.sdk.all@81.0.0`（`Packages/manifest.json`）
- MR Utility Kit（MRUK）：`com.meta.xr.mrutilitykit@81.0.0`（`Packages/packages-lock.json`）
- URP：`com.unity.render-pipelines.universal@17.2.0`（`Packages/manifest.json`）
- XR：OpenXR + XR Management（`com.unity.xr.openxr@1.15.1`、`com.unity.xr.management@4.5.3`）
- Input System：`com.unity.inputsystem@1.14.2`

Android Player Settings（目前專案值）：
- `minSdkVersion=32`、`targetSdkVersion=32`（`ProjectSettings/ProjectSettings.asset`）

> 上架/提交風險提醒（政策變更）：Meta 已公告自 **2026-03-01** 起，Meta Horizon/Quest 的新 binary 上傳需 **target Android 14（API 34）**；minSdkVersion 仍可維持 API 32。若你們以「可上架」為目標，請及早把 target SDK 拉到 34，並完成一次完整 build 驗證與回歸測試。
>
> 參考：Meta Developer Blog — “Meta Horizon Apps Must Target Android 14 Starting March 1”  
> https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/

---

## 3. 安裝與執行（APK / Source）

### A. 安裝 APK（直接玩）
適用：你已拿到 `GhostHunter.apk`（或助教提供之 APK）。

1. 開啟 Quest Developer Mode（Meta Quest App → 裝置 → Developer Mode）
2. USB 連線 Quest 與電腦，戴上頭顯確認「允許 USB 偵錯（USB Debugging）」
3. 擇一安裝方式：

**方式 1：ADB（建議）**
```bash
adb devices
adb install -r GhostHunter.apk
```

**方式 2：SideQuest**
- 開啟 SideQuest → 連線成功 → Install APK → 選擇 `GhostHunter.apk`

4. Quest：Library → Unknown Sources → 啟動本遊戲

### B. 從 Unity 專案建置（Source Build）
1. Unity Hub 開啟專案根目錄（必須包含 `Assets/`、`Packages/`、`ProjectSettings/`）
2. 開啟場景：`Assets/Scenes/SampleScene.unity`
3. Layer（驗收關鍵）
   - Layer 3：`Enemy`（Ghost 在此 layer；RayGun 以 layerMask 命中）
   - Layer 6：`Tower`（Tower 在此 layer；RayGun 以 occlusionMask 遮擋）
4. File → Build Settings
   - Platform：Android（Switch Platform）
   - Scenes In Build：加入 `SampleScene`
5. Build And Run（Quest 需 USB 連線並已允許偵錯）

---

## 4. 操作說明（Quest 右手）
| 動作 | 裝置/按鍵 | Source of Truth |
|---|---|---|
| 射擊 | 右手食指扳機（R Index Trigger） | `RayGun.shootingButton = OVRInput.RawButton.RIndexTrigger` |
| 重新開始 | Game Over UI 的 Restart 按鈕 | `GameManager.RestartGame()`（UI Button OnClick 呼叫） |

---

## 5. 驗收 Checklist（建議逐項勾選）
- [ ] 完成 MR 掃描後，塔會放在「玩家前方桌面」且 5–10 秒內不漂移到遠處  
- [ ] 桌面持續生成 Ghost（至少 1 隻；同時存在不超過 `maxAlive`）  
- [ ] 右手扳機射擊：命中 Ghost 會扣血並可擊殺  
- [ ] 放任 Ghost 碰塔：塔 HP 下降（Console 可見 `[Tower] HP: ...`）  
- [ ] 塔 HP=0：觸發 Game Over（Spawner/Gun 停用、清場、顯示 Game Over UI）  

---

## 6. 常見失敗模式與快速排查（TA/口試友善）
1) **塔漂移到遠處**  
   - 建議延長 warmup / 增加 retry；並確認 MR 掃描完成後再進入遊戲流程（參考 `docs/SDD.md` 之風險章節）。

2) **桌面不生成 Ghost**  
   - 常見原因：桌面 label 不穩或光線/反光造成表面辨識不佳。  
   - 對策：維持 `TABLE | OTHER` 的容錯設定；重新掃描房間；更換桌面位置或改善照明。

3) **射擊近距離 miss**  
   - 本專案已用 overlap + spherecast 增加穩定；若仍不穩，優先檢查 Enemy layer 與 RayGun layerMask 是否一致。

4) **Game Over 後仍能射擊/生成**  
   - 這屬於收斂性缺陷；請確認 `GameManager` 的 disable/cleanup 流程只觸發一次且無重入。

---

## 7. 專案結構（最低必要）
- `Assets/`：Unity 資產、場景、腳本、Prefab
- `docs/`：設計文件（SDD）與 Spike 報告
- `Packages/`、`ProjectSettings/`：專案設定（版本與可重現依據）

---

## 8. 文件入口
- `docs/SDD.md`：Software Design Document（需求/設計/驗收口徑/風險）
- `docs/TechnicalSpike.md`：Technical Spike 報告（方法/量測/結果/討論模板）

