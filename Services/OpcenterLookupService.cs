using System.Data;
using HALRouteCardApp.Models;
using Microsoft.Data.SqlClient;

namespace HALRouteCardApp.Services;

public interface IOpcenterLookupService
{
    Task<RouteCardLookupResult?> FindAsync(RouteCardLookupRequest request, CancellationToken cancellationToken);
}

public sealed class OpcenterLookupService(IConfiguration configuration) : IOpcenterLookupService
{
    private static readonly string[] OrderColumns = ["ORDER_NO", "SHOP_ORDER_NO", "ORDER_NUMBER", "ORDERNUMBER", "LOT_ORDER_NO", "CUSTOMER_ORDER_NO"];
    private static readonly string[] WorkOrderColumns = ["WORK_ORDER_NO", "WORKORDER_NO", "WORK_ORDER", "WORKORDER"];
    private static readonly string[] PartColumns = ["PART_NO", "PARTNO", "ITEM_NO", "ITEMNUMBER", "MATERIAL_NO"];
    private static readonly string[] PartNameColumns = ["PART_NAME", "PARTNAME", "ITEM_NAME", "ITEM_DESC", "DESCRIPTION", "PART_DESCRIPTION", "NAME"];
    private static readonly string[] QtyColumns = ["SPLIT_QTY", "SPLIT_QUANTITY", "JC_QTY", "JOB_QTY", "QTY", "QUANTITY", "ORDER_QTY", "SHOP_ORDER_QTY", "REVISED_QTY_DUE", "ORG_QTY_DUE"];
    private static readonly string[] PslColumns = ["PSL_NO", "PSLNO", "PSL"];

    private const string DefaultProductAndRoutingSql = """
        SELECT
            P.PartNo,
            P.OpNo,
            CONCAT(P.PartNo, '-', P.OpNo) AS Uid,
            'A' AS Revision,
            (P.OpTimePerItem * 3600) AS OpTimePerItemInSeconds,
            R.NOTE_TEXT AS NoteText,
            WC.department_no AS WorkcenterDepartment
        FROM UserData.Products P
        JOIN APS_ROUTING_OPERATION R
            ON P.PartNo = R.PART_NO
           AND P.OpNo = R.OPERATION_NO
        LEFT JOIN APS_Work_center WC
            ON R.WORK_CENTER_NO = WC.WORK_CENTER_NO
        WHERE P.PartNo = @PartNo
        ORDER BY P.OpNo;
        """;

    public async Task<RouteCardLookupResult?> FindAsync(RouteCardLookupRequest request, CancellationToken cancellationToken)
    {
        var (searchValue, searchColumns, isPartNumber) = GetSearch(request);
        var result = new RouteCardLookupResult();

        foreach (var (connectionName, databaseName) in new[] { ("Opcenter", "OpcenterAPSHAL"), ("HALFNF", "HALFNF"), ("HALNEW", "HALNEW") })
        {
            var connectionString = configuration.GetConnectionString(connectionName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{connectionName} is not configured.");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await FindAcrossTablesAsync(connection, databaseName, searchValue, searchColumns, isPartNumber, result, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result.PartNo))
                await ReadOperationsAsync(connection, databaseName, result, cancellationToken);
        }

        result.PartNo ??= request.PartNo?.Trim();
        return result.Records.Count == 0 && result.Operations.Count == 0 ? null : result;
    }

    private static (string Value, string[] Columns, bool IsPartNumber) GetSearch(RouteCardLookupRequest request)
    {
        foreach (var candidate in new[]
        {
            (request.OrderNo, OrderColumns),
            (request.WorkOrderNo, WorkOrderColumns),
            (request.PartNo, PartColumns)
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate.Item1))
                return (candidate.Item1.Trim(), candidate.Item2, ReferenceEquals(candidate.Item2, PartColumns));
        }

        throw new ArgumentException("Enter an order number, work order number, or part number.", nameof(request));
    }

    private async Task FindAcrossTablesAsync(SqlConnection connection, string databaseName, string searchValue, IEnumerable<string> searchCandidates, bool isPartNumber, RouteCardLookupResult result, CancellationToken cancellationToken)
    {
        var tables = await DiscoverTablesAsync(connection, searchCandidates, cancellationToken);

        foreach (var table in tables)
        {
            var searchColumns = table.Columns.Intersect(searchCandidates, StringComparer.OrdinalIgnoreCase);
            foreach (var searchColumn in searchColumns)
            {
                var rows = await ReadMatchingRowsAsync(connection, table, searchColumn, searchValue, isPartNumber, cancellationToken);
                foreach (var row in rows)
                {
                    result.SourceTable ??= table.QualifiedName;
                    if (!result.SourceDatabases.Contains(databaseName, StringComparer.OrdinalIgnoreCase))
                        result.SourceDatabases.Add(databaseName);
                    var sourceTable = $"{databaseName}.{table.QualifiedName}";
                    if (!result.SourceTables.Contains(sourceTable, StringComparer.OrdinalIgnoreCase))
                        result.SourceTables.Add(sourceTable);
                    result.Records.Add(new RouteCardLookupRecord
                    {
                        Database = databaseName,
                        Table = table.QualifiedName,
                        Fields = row.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString(), StringComparer.OrdinalIgnoreCase)
                    });
                    result.PartNo ??= ReadString(row, PartColumns);
                    result.PartName ??= ReadString(row, PartNameColumns);
                    result.PslNo ??= ReadString(row, PslColumns);
                    result.OrderNo ??= ReadString(row, OrderColumns);
                    result.WorkOrderNo ??= ReadString(row, WorkOrderColumns);
                    result.JcQty ??= ReadDecimal(row, ["SPLIT_QTY", "SPLIT_QUANTITY", "JC_QTY", "JOB_QTY", "REVISED_QTY_DUE", "ORG_QTY_DUE"]);
                    result.Qty ??= ReadDecimal(row, QtyColumns);
                    AddMatchedFields(result.MatchedFields, row);
                }
            }
        }
    }

    private async Task<List<TableInfo>> DiscoverTablesAsync(SqlConnection connection, IEnumerable<string> searchCandidates, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
            """;
        var candidates = searchCandidates.ToArray();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            var key = schema + "." + name;
            if (!tables.TryGetValue(key, out var table))
            {
                table = new TableInfo(schema, name);
                tables[key] = table;
            }
            table.Columns.Add(reader.GetString(2));
        }
        return tables.Values.Where(table => table.Columns.Any(column => candidates.Contains(column, StringComparer.OrdinalIgnoreCase))).ToList();
    }

    private static async Task<List<Dictionary<string, object?>>> ReadMatchingRowsAsync(SqlConnection connection, TableInfo table, string column, string value, bool normalizePartNumber, CancellationToken cancellationToken)
    {
        var escapedColumn = "[" + column.Replace("]", "]]", StringComparison.Ordinal) + "]";
        string matchExpression;
        if (normalizePartNumber)
        {
            matchExpression = $"UPPER(REPLACE(REPLACE(CONVERT(nvarchar(4000), {escapedColumn}), '-', ''), ' ', ''))";
            value = new string(value.Where(character => !char.IsWhiteSpace(character) && character != '-').ToArray()).ToUpperInvariant();
        }
        else
        {
            matchExpression = $"CONVERT(nvarchar(4000), {escapedColumn})";
        }
        var sql = $"SELECT TOP (25) * FROM {table.QualifiedName} WHERE {matchExpression} = @SearchValue;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@SearchValue", SqlDbType.NVarChar, 4000) { Value = value });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(row);
        }
        return rows;
    }

    private async Task ReadOperationsAsync(SqlConnection connection, string databaseName, RouteCardLookupResult result, CancellationToken cancellationToken)
    {
        var configuredSql = configuration["OpcenterQueries:ProductAndRouting"];
        var sql = string.IsNullOrWhiteSpace(configuredSql)
            ? await HasRoutingTablesAsync(connection, cancellationToken)
                ? DefaultProductAndRoutingSql
                : """
                    SELECT
                        P.PartNo,
                        P.OpNo,
                        CONCAT(P.PartNo, '-', P.OpNo) AS Uid,
                        'A' AS Revision,
                        (P.OpTimePerItem * 3600) AS OpTimePerItemInSeconds,
                        P.OperationName AS NoteText,
                        CAST(NULL AS nvarchar(100)) AS WorkcenterDepartment
                    FROM UserData.Products P
                    WHERE P.PartNo = @PartNo
                    ORDER BY P.OpNo;
                    """
            : configuredSql;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@PartNo", SqlDbType.NVarChar, 100) { Value = result.PartNo! });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (result.Operations.Any(op => string.Equals(op.PartNo, ReadString(reader, "PartNo"), StringComparison.OrdinalIgnoreCase) && string.Equals(op.OpNo, ReadString(reader, "OpNo"), StringComparison.OrdinalIgnoreCase)))
                continue;
            result.Operations.Add(new RouteCardOperationLookup
            {
                PartNo = ReadString(reader, "PartNo"),
                OpNo = ReadString(reader, "OpNo"),
                Uid = ReadString(reader, "Uid"),
                OperationName = ReadString(reader, "NoteText"),
                Revision = ReadString(reader, "Revision") ?? "A",
                OpTimePerItemInSeconds = ReadDecimal(reader, "OpTimePerItemInSeconds"),
                NoteText = ReadString(reader, "NoteText"),
                WorkcenterDepartment = ReadString(reader, "WorkcenterDepartment")
            });
        }
    }

    private static void AddMatchedFields(Dictionary<string, string?> target, Dictionary<string, object?> row)
    {
        foreach (var pair in row)
            if (pair.Value is not null && !target.ContainsKey(pair.Key))
                target[pair.Key] = Convert.ToString(pair.Value);
    }

    private static string? ReadString(Dictionary<string, object?> row, IEnumerable<string> names) =>
        names.Select(name => row.TryGetValue(name, out var value) ? value : null).FirstOrDefault(value => value is not null)?.ToString();

    private static string? ReadString(SqlDataReader reader, string name) =>
        reader.GetOrdinal(name) is var ordinal && reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));

    private static decimal? ReadDecimal(Dictionary<string, object?> row, IEnumerable<string> names)
    {
        var value = names.Select(name => row.TryGetValue(name, out var item) ? item : null).FirstOrDefault(item => item is not null);
        return value is null ? null : Convert.ToDecimal(value);
    }

    private static decimal? ReadDecimal(SqlDataReader reader, string name) =>
        reader.GetOrdinal(name) is var ordinal && reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));

    private static async Task<bool> HasRoutingTablesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'dbo.APS_ROUTING_OPERATION', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.APS_WORK_CENTER', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private sealed class TableInfo(string schema, string name)
    {
        public string QualifiedName => "[" + schema.Replace("]", "]]", StringComparison.Ordinal) + "].[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";
        public List<string> Columns { get; } = [];
    }
}
