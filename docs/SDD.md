# MR 桌面塔防（Technical Spike）— SDD（Software Design Document）

## 1. 目標與範圍
本專案為 Meta Quest（MR）上的桌面塔防小遊戲。玩家在混合實境中以射線槍擊殺於桌面隨機生成之怪物；怪物接觸高塔則扣血，高塔血量歸零即遊戲結束。

**Spike（技術穿刺）範圍**：不做選單、不做美術擴充；僅驗證「核心技術」在 Quest 上可穩定運行：  
- MR 桌面（水平面）辨識 / 取樣點生成  
- 生成敵人 Prefab 於桌面表面  
- 玩家射線槍輸入與命中判定  
- 怪物→高塔碰撞扣血與 Game Over 條件

## 2. 使用者故事（User Story）
- 玩家戴上 Quest 後，系統辨識真實桌面並於桌面上隨機生成怪物。
- 玩家使用右手射線槍瞄準並射擊怪物以將其消滅。
- 若怪物接觸到桌面中央的高塔，則高塔扣血；當高塔 HP 歸零時遊戲結束並停止生成與射擊。

## 3. 核心規格（Gameplay Spec）
### 3.1 遊戲規則（對照期末專題規格）
1. 怪物會在桌面上隨機生成  
2. 玩家必須以槍枝擊殺怪物  
3. 若讓怪物碰到高塔則會扣除一定血量  
4. 高塔血量歸零則結束  

### 3.2 最小可交付（MVP / Spike）定義
- 生成：每 N 秒生成 1 隻怪物（或單次生成 K 隻），位置取樣自 MR 的水平表面（桌面）。
- 擊殺：射線命中怪物即可造成 1 點傷害（Spike 建議 1 槍擊殺）。
- 扣血：怪物進入高塔 Trigger 時扣固定血量（例如 10），怪物即銷毀。
- 結束：HP=0 時停用 Spawner 與 RayGun（或進入 GameOver 狀態）。

## 4. 系統架構（Architecture）
### 4.1 現有資產（已在專案中）
- `Assets/Scripts/GhostSpawer.cs`：使用 Meta XR MRUtilityKit（MRUK）取樣表面點生成 Ghost Prefab  
- `Assets/Scripts/RayGun.cs`：右手扳機輸入、Raycast、命中特效與音效  
- Prefab：`Assets/Prefabs/Ghost.prefab`、`Assets/Prefabs/Red Line.prefab`、`Assets/Prefabs/Ray Impact.prefab`  
- 場景：`Assets/Scenes/SampleScene.unity`

### 4.2 Spike 需補齊模組
- `GhostEnemy`：敵人移動、受傷、死亡、觸塔造成傷害
- `TowerHealth`：高塔血量、扣血、GameOver
- （可選）`GameManager`：遊戲狀態（Playing / GameOver）、UI 與重開

## 5. 元件設計（Component Design）
### 5.1 GhostSpawner（生成）
**責任**：在 Quest MR 空間中，從 MRUK 的「水平面（桌面）」隨機取樣生成敵人。

**輸入**：  
- MRUK 房間 / 表面資料（MRUKRoom）  
- spawnLabels（可用於桌面 label 篩選）  
- 生成參數：生成數量、生成間隔、最小邊緣距離、法線偏移

**輸出**：  
- 在世界座標中 Instantiate `Ghost.prefab`  
- 生成後將 `tower` 目標注入到 `GhostEnemy`

### 5.2 RayGun（射擊）
**責任**：偵測右手扳機，發射 Raycast，命中時產生回饋並對命中物件造成傷害。

**命中邏輯（Spike）**：  
- `RaycastHit.collider.GetComponentInParent<IDamageable>()` 存在則 `TakeDamage(1)`

### 5.3 GhostEnemy（敵人）
**責任**：朝高塔水平移動；被射線傷害；碰到高塔 Trigger 時扣塔血並自毀。

**資料**：hp、moveSpeed、damageToTower、tower(Transform)

### 5.4 TowerHealth（高塔）
**責任**：維護 hp/maxHp；被扣血後判定 GameOver；停用生成與射擊。

## 6. 技術風險（Technical Risk）
| 風險 | 說明 | 對策（Spike 驗證） |
|---|---|---|
| MR 桌面辨識穩定度 | 桌面掃描品質、光線、反光材質可能影響 | 以 MRUK 水平面取樣；加入重試與 fallback（延遲再試） |
| 生成姿態不正確 | 桌面法線接近 (0,1,0) 時 LookRotation 可能退化 | 桌面生成採固定 Upright + Random Yaw |
| Raycast 命中錯層 | 射線 layerMask 沒包含敵人層 | 建議建立 Enemy Layer 並於 RayGun layerMask 勾選 |
| Trigger 不觸發 | Collider / Rigidbody 組合不完整 | Enemy 使用 SphereCollider（IsTrigger）+ 具備 Rigidbody（kinematic） |

## 7. 測試計畫（Test Plan）
- 場景啟動 → MRUK 完成掃描 → 桌面出現敵人（>=1）  
- 右手扳機 → 射線可見 + 命中敵人後敵人消失（或 hp 減少）  
- 敵人接觸 Tower → Tower HP 下降（Console 或 UI）  
- Tower HP 歸零 → Spawner / RayGun 停用，顯示 Game Over 訊息

## 8. 交付物（Deliverables）
- GitHub Repo（Unity 專案 + `docs/SDD.md`）  
- Spike 證據：Quest 實機錄影或截圖（建議放 `docs/media/` 並在 README 連結）
