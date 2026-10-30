using System.Reflection;
using Gallop;
using UmamusumeResponseAnalyzer.PluginTesting;

static class SourceDtoFactory
{
    public static object Create(Type type, string salt)
    {
        var value = RandomDtoGenerator.Create(type, salt, RandomDtoProfile.Analyzer);
        if (type.Name.EndsWith("CheckEventResponse", StringComparison.Ordinal) || type.Name.EndsWith("RaceEndOutResponse", StringComparison.Ordinal))
            StabilizeCheckEventResponse(value);
        if (type.Name.EndsWith("ExecCommandResponse", StringComparison.Ordinal))
            StabilizeExecCommandResponse(value);
        if (type.Name.EndsWith("RaceEndResponse", StringComparison.Ordinal))
            StabilizeRaceEndResponse(value);
        if (type.Name.StartsWith("SingleModeRamen", StringComparison.Ordinal) && type.Name.EndsWith("Response", StringComparison.Ordinal)) StabilizeRamenResponse(value);
        return value;
    }

    static void StabilizeCheckEventResponse(object value)
    {
        var dataField = value.GetType().GetField("data", BindingFlags.Instance | BindingFlags.Public);
        var data = dataField?.GetValue(value);
        if (data is null)
            return;

        SetArrayFieldEmpty(data, "unchecked_event_array");
        SetArrayFieldEmpty(data, "select_index_info_array");

        var charaInfo = (SingleModeChara?)data.GetType().GetField("chara_info", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (charaInfo is not null)
        {
            charaInfo.state = 1;
            charaInfo.turn = 1;
            charaInfo.skill_array = [];
            charaInfo.skill_tips_array = [];
            charaInfo.support_card_array = [];
            StabilizeProperFields(charaInfo);
        }

    }

    static void StabilizeExecCommandResponse(object value)
    {
        var data = value.GetType().GetField("data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        if (data is null)
            return;

        SetArrayFieldEmpty(data, "unchecked_event_array");

        var commandResult = data.GetType().GetField("command_result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (commandResult is not null)
            SetIntField(commandResult, "result_state", 0);

        var charaInfo = (SingleModeChara?)data.GetType().GetField("chara_info", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (charaInfo is not null)
        {
            charaInfo.state = 1;
            charaInfo.turn = 1;
            charaInfo.skill_array = [];
            charaInfo.skill_tips_array = [];
            charaInfo.support_card_array = [];
            StabilizeProperFields(charaInfo);
        }


    }

    static void StabilizeRaceEndResponse(object value)
    {
        var data = value.GetType().GetField("data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        if (data is null)
            return;

        SetArrayFieldEmpty(data, "race_history");
    }

    static void StabilizeTrainingChara(SingleModeChara charaInfo, int[] trainIds, int[] evaluationIds)
    {
        charaInfo.state = 1;
        charaInfo.playing_state = 1;
        charaInfo.turn = 1;
        charaInfo.motivation = 5;
        charaInfo.vital = 80;
        charaInfo.max_vital = 100;
        charaInfo.speed = charaInfo.stamina = charaInfo.power = charaInfo.guts = charaInfo.wiz = 500;
        charaInfo.max_speed = charaInfo.max_stamina = charaInfo.max_power = charaInfo.max_guts = charaInfo.max_wiz = 1200;
        charaInfo.skill_point = 1200;
        charaInfo.support_card_array = [.. Enumerable.Range(1, 6).Select(position => new SingleModeSupportCard
        {
            position = position,
            support_card_id = 10000 + position
        })];
        charaInfo.evaluation_info_array = [.. evaluationIds.Select(targetId => new EvaluationInfo
        {
            target_id = targetId,
            evaluation = 80
        })];
        charaInfo.training_level_info_array = [.. trainIds.Select(commandId => new TrainingLevelInfo
        {
            command_id = commandId,
            level = 1
        })];
    }

    static void StabilizeProperFields(SingleModeChara charaInfo)
    {
        charaInfo.proper_distance_short = charaInfo.proper_distance_mile =
            charaInfo.proper_distance_middle = charaInfo.proper_distance_long = 1;
        charaInfo.proper_running_style_nige = charaInfo.proper_running_style_oikomi =
            charaInfo.proper_running_style_sashi = charaInfo.proper_running_style_senko = 1;
        charaInfo.proper_ground_turf = charaInfo.proper_ground_dirt = 1;
    }

    static void StabilizeHomeCommandInfo(SingleModeHomeInfo homeInfo, int[]? trainIds = null)
    {
        trainIds ??= [101, 105, 102, 103, 106];
        homeInfo.command_info_array = [.. trainIds.Select((commandId, index) => new SingleModeCommandInfo
        {
            command_type = 1,
            command_id = commandId,
            is_enable = 1,
            failure_rate = index * 5,
            training_partner_array = [.. Enumerable.Range(1, index % 3 + 1)],
            tips_event_partner_array = [],
            params_inc_dec_info_array = CreateParams(commandId, includeVital: true)
        })];
    }

    static SingleModeParamsIncDecInfo[] CreateParams(int baseValue, bool includeVital)
    {
        var targetTypes = includeVital ? new[] { 1, 2, 3, 4, 5, 30, 10 } : [1, 2, 3, 4, 5, 30];
        return [.. targetTypes.Select((targetType, index) => new SingleModeParamsIncDecInfo
        {
            target_type = targetType,
            value = targetType == 10 ? -10 : baseValue + index
        })];
    }

    static void StabilizeArrayField(object value, string fieldName, int length, Action<object, int> stabilize)
    {
        var field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field?.FieldType.IsArray != true)
            return;

        var elementType = field.FieldType.GetElementType()!;
        var array = Array.CreateInstance(elementType, length);
        for (var i = 0; i < length; i++)
        {
            var item = Activator.CreateInstance(elementType)
                ?? throw new InvalidOperationException($"Cannot create DTO array value: {elementType.FullName}");
            stabilize(item, i);
            array.SetValue(item, i);
        }

        field.SetValue(value, array);
    }

    static void SetIntField(object value, string fieldName, int fieldValue)
    {
        var field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field is not null && field.FieldType == typeof(int))
            field.SetValue(value, fieldValue);
    }

    static void SetArrayFieldEmpty(object value, string fieldName)
    {
        var field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field?.FieldType.IsArray != true)
            return;

        field.SetValue(value, Array.CreateInstance(field.FieldType.GetElementType()!, 0));
    }

    static void SetArrayValue(object value, string fieldName, Array fieldValue)
    {
        var field = value.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field?.FieldType.IsArray != true)
            return;

        field.SetValue(value, fieldValue);
    }

    static void StabilizeRamenResponse(object value)
    {
        var data = value.GetType().GetField("data", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        if (data is null)
            return;

        SetArrayFieldEmpty(data, "unchecked_event_array");

        var charaInfo = (SingleModeChara?)data.GetType().GetField("chara_info", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (charaInfo is not null)
        {
            StabilizeTrainingChara(charaInfo, [101, 105, 102, 103, 106], [1, 2, 3, 4, 5, 6]);
        }

        var homeInfo = (SingleModeHomeInfo?)data.GetType().GetField("home_info", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (homeInfo is not null)
            StabilizeHomeCommandInfo(homeInfo);

        var ramenDataSet = data.GetType().GetField("ramen_data_set", BindingFlags.Instance | BindingFlags.Public)?.GetValue(data);
        if (ramenDataSet is not null)
            StabilizeRamenDataSet(ramenDataSet);
    }

    static void StabilizeRamenDataSet(object ramenDataSet)
    {
        var trainIds = new[] { 101, 105, 102, 103, 106 };
        StabilizeArrayField(ramenDataSet, "command_info_array", trainIds.Length, (item, index) =>
        {
            SetIntField(item, "command_type", 1);
            SetIntField(item, "command_id", trainIds[index]);
            SetArrayValue(item, "params_inc_dec_info_array", CreateParams(trainIds[index] + 10, includeVital: false));
        });
        StabilizeArrayField(ramenDataSet, "command_feeling_info_array", trainIds.Length, (item, index) =>
        {
            SetIntField(item, "command_type", 1);
            SetIntField(item, "command_id", trainIds[index]);
            SetIntField(item, "feeling_id", index + 1);
        });
        StabilizeArrayField(ramenDataSet, "training_exec_info_array", trainIds.Length, (item, index) =>
        {
            SetIntField(item, "base_command_id", trainIds[index]);
            SetIntField(item, "exec_count", index);
        });
        StabilizeArrayField(ramenDataSet, "feeling_info_array", 2, (item, index) =>
        {
            SetIntField(item, "feeling_index", index + 1);
            SetIntField(item, "feeling_id", index + 1);
        });
        StabilizeArrayField(ramenDataSet, "feeling_turn_info_array", 2, (item, index) =>
        {
            SetIntField(item, "feeling_id", index + 1);
            SetIntField(item, "remain_turn", 3 - index);
        });
        StabilizeArrayField(ramenDataSet, "feeling_reduce_turn_info_array", trainIds.Length, (item, index) =>
        {
            SetIntField(item, "command_type", 1);
            SetIntField(item, "command_id", trainIds[index]);
            StabilizeArrayField(item, "feeling_turn_array", 2, (feelingTurn, feelingIndex) =>
            {
                SetIntField(feelingTurn, "feeling_id", feelingIndex + 1);
                SetIntField(feelingTurn, "turn", 1);
            });
        });
    }

}
