using Gallop;

namespace RamenScenarioAnalyzer;

/// <summary>
/// 拉面杯剧本状态门面：单进程全局唯一，读写经 <see cref="Gate"/> 同步，
/// 消费方通过 <see cref="Snapshot"/> 读取不可变副本。
/// 只有 Load 能把 loaded 置 true，插件 Dispose 时调用 <see cref="Clear"/>；
/// 带角色标识的部分更新跨育成时先清空旧状态。消费方发现 snapshot 的 charaId
/// 与当前响应不一致时，说明是新的一局，应忽略旧 snapshot。
/// </summary>
public static class RamenScenarioState
{
    static readonly object Gate = new();
    static bool loaded;
    static int single_mode_chara_id;
    static int[] selected_region_id_array = new int[3];
    static int[] reduce_base_turn = new int[3]; // guage_base_distribution
    // 待确认 check_point_info_array
    static int last_ramen;
    static int check_point_pt;
    static int expected_check_point_pt;
    // select_region 请求写入后、被带 charaId 的更新确认前为 true；
    // 跨育成复位时据此保留请求写入的值。
    static bool region_select_pending;

    /// <summary>
    /// 由 Load 响应写入全部状态（唯一携带完整数据的路径）。
    /// </summary>
    /// <param name="charaId">当前局的 <c>single_mode_chara_id</c>。</param>
    /// <param name="data">响应中的 <c>ramen_data_set_load</c>。</param>
    public static void UpdateLoad(int charaId, SingleModeRamenDataSetLoad data)
    {
        if (data is null)
            return;

        lock (Gate)
        {
            single_mode_chara_id = charaId;
            selected_region_id_array = data.selected_region_id_array ?? new int[3];

            Array.Clear(reduce_base_turn, 0, reduce_base_turn.Length);
            if (data.reduce_base_turn_info_array is not null)
                foreach (var info in data.reduce_base_turn_info_array)
                {
                    var idx = info.feeling_id - 1;
                    if (idx >= 0 && idx < reduce_base_turn.Length)
                        reduce_base_turn[idx] = info.reduce_base_turn;
                }

            // last_tasting_info == null 表示未吃过，last_ramen 记为 -1。
            last_ramen = data.last_tasting_info is null
                ? -1
                : data.last_tasting_info.region_id;

            check_point_pt = data.check_point_pt;
            expected_check_point_pt = data.expected_check_point_pt;

            loaded = true;
            region_select_pending = false;
        }
    }

    /// <summary>
    /// 由地区选择响应更新 <c>selected_region_id_array</c> 与 charaId。
    /// </summary>
    public static void UpdateRegionSelect(int charaId, SingleModeRamenDataSetLoad data)
    {
        if (data is null)
            return;

        lock (Gate)
        {
            if (single_mode_chara_id != charaId)
                Clear();
            single_mode_chara_id = charaId;
            selected_region_id_array = data.selected_region_id_array ?? new int[3];
            region_select_pending = false;
        }
    }

    /// <summary>
    /// 由品尝响应写入 <c>last_ramen</c>、<c>check_point_pt</c>、<c>expected_check_point_pt</c>
    /// 并刷新角色标识。角色标识不一致时先复位旧状态，但 pending 的地区选择请求
    /// 已写入的 <c>selected_region_id_array</c> 与 <c>reduce_base_turn</c> 予以保留。
    /// </summary>
    /// <param name="charaId"><c>chara_info.single_mode_chara_id</c>。</param>
    /// <param name="lastTastingInfo">为 null 时 <c>last_ramen</c> 记为 -1（未吃过）。</param>
    public static void UpdateTasting(
        int charaId,
        SingleModeRamenLastTastingInfo lastTastingInfo,
        int checkPointPt,
        int expectedCheckPointPt)
    {
        lock (Gate)
        {
            if (single_mode_chara_id != charaId)
            {
                if (region_select_pending)
                {
                    // 请求写入的值属于当前这局：仅复位其余状态。
                    var selectedRegions = selected_region_id_array;
                    var reduceBaseTurn = reduce_base_turn;
                    Clear();
                    selected_region_id_array = selectedRegions;
                    reduce_base_turn = reduceBaseTurn;
                }
                else
                {
                    Clear();
                }
            }
            region_select_pending = false;
            single_mode_chara_id = charaId;
            last_ramen = lastTastingInfo is null
                ? -1
                : lastTastingInfo.region_id;
            check_point_pt = checkPointPt;
            expected_check_point_pt = expectedCheckPointPt;
        }
    }

    /// <summary>
    /// 由地区选择请求写入 <c>selected_region_id_array</c>，并把 <c>check_point_pt</c>
    /// 复位为 0（新一轮检查点累积，<c>expected_check_point_pt</c> 不变）。
    /// 请求不携带 charaId，写入标记为 pending，跨育成品尝复位时保留写入值。
    /// </summary>
    /// <param name="regionIdArray">请求体中的 <c>region_id_array</c>，为 null 时跳过。</param>
    public static void UpdateRegionSelect(int[] regionIdArray)
    {
        if (regionIdArray is null)
            return;

        lock (Gate)
        {
            selected_region_id_array = (int[])regionIdArray.Clone();
            check_point_pt = 0;
            region_select_pending = true;
        }
    }

    /// <summary>
    /// 在锁内拷贝全部字段，构造一份不可变快照。
    /// </summary>
    public static RamenStateSnapshot Snapshot()
    {
        lock (Gate)
        {
            return new RamenStateSnapshot(
                loaded,
                single_mode_chara_id,
                (int[])selected_region_id_array.Clone(),
                (int[])reduce_base_turn.Clone(),
                last_ramen,
                check_point_pt,
                expected_check_point_pt);
        }
    }

    /// <summary>
    /// 把所有字段复位为初始值；线程安全。
    /// </summary>
    public static void Clear()
    {
        lock (Gate)
        {
            loaded = false;
            single_mode_chara_id = 0;
            selected_region_id_array = new int[3];
            reduce_base_turn = new int[3];
            last_ramen = 0;
            check_point_pt = 0;
            expected_check_point_pt = 0;
            region_select_pending = false;
        }
    }
}

/// <summary>
/// 不可变快照，内部数组已 Clone；经 <see cref="RamenScenarioState.Snapshot"/> 获取。
/// </summary>
public sealed class RamenStateSnapshot
{
    public RamenStateSnapshot(
        bool loaded,
        int single_mode_chara_id,
        int[] selected_region_id_array,
        int[] reduce_base_turn,
        int last_ramen,
        int check_point_pt,
        int expected_check_point_pt)
    {
        this.loaded = loaded;
        this.single_mode_chara_id = single_mode_chara_id;
        this.selected_region_id_array = selected_region_id_array;
        this.reduce_base_turn = reduce_base_turn;
        this.last_ramen = last_ramen;
        this.check_point_pt = check_point_pt;
        this.expected_check_point_pt = expected_check_point_pt;
    }

    public bool loaded { get; }
    public int single_mode_chara_id { get; }
    public int[] selected_region_id_array { get; }
    public int[] reduce_base_turn { get; }
    public int last_ramen { get; }
    public int check_point_pt { get; }
    public int expected_check_point_pt { get; }
}
