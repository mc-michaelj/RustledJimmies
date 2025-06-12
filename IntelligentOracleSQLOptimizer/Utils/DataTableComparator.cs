using System;
using System.Data;

namespace IntelligentOracleSQLOptimizer.Utils
{
    public static class DataTableComparator
    {
        public static bool AreTablesEqual(DataTable? table1, DataTable? table2)
        {
            // Initial Checks
            if (table1 == null && table2 == null)
                return true;
            if (table1 == null || table2 == null)
                return false;

            // Schema Comparison: Column Count
            if (table1.Columns.Count != table2.Columns.Count)
                return false;

            // Schema Comparison: Column Names and DataTypes
            for (int i = 0; i < table1.Columns.Count; i++)
            {
                if (table1.Columns[i].ColumnName != table2.Columns[i].ColumnName)
                    return false;
                if (table1.Columns[i].DataType != table2.Columns[i].DataType)
                    return false;
            }

            // Row Count Comparison
            if (table1.Rows.Count != table2.Rows.Count)
                return false;

            // Row Data Comparison
            for (int i = 0; i < table1.Rows.Count; i++)
            {
                for (int j = 0; j < table1.Columns.Count; j++)
                {
                    object? value1 = table1.Rows[i][j];
                    object? value2 = table2.Rows[i][j];

                    // Handle DBNull.Value specifically
                    bool value1IsDbNull = (value1 == DBNull.Value || value1 == null); // Treat null and DBNull as potentially equivalent for this check if needed, though DBNull is more precise
                    bool value2IsDbNull = (value2 == DBNull.Value || value2 == null);

                    if (value1IsDbNull && value2IsDbNull)
                        continue; // Both are DBNull (or null), so they are equal for this cell

                    if (value1IsDbNull || value2IsDbNull)
                        return false; // One is DBNull (or null), the other isn't

                    if (!object.Equals(value1, value2))
                    {
                        // Optional: Add detailed logging here if values don't match
                        // Console.WriteLine($"Mismatch at Row {i}, Column {j}: '{value1}' vs '{value2}'");
                        return false;
                    }
                }
            }

            // If all checks pass
            return true;
        }
    }
}
