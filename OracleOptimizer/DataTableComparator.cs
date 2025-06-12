using System;
using System.Data;
using System.Linq;

namespace OracleOptimizer
{
    public static class DataTableComparator
    {
        /// <summary>
        /// Compares two DataTable objects for equality in terms of structure and data.
        /// </summary>
        /// <param name="table1">The first DataTable.</param>
        /// <param name="table2">The second DataTable.</param>
        /// <returns>True if the tables are equal, false otherwise.</returns>
        public static bool AreTablesEqual(DataTable table1, DataTable table2)
        {
            if (table1 == null && table2 == null)
                return true;
            if (table1 == null || table2 == null)
                return false;

            // Check column count and names (order matters for simplicity here)
            if (table1.Columns.Count != table2.Columns.Count)
                return false;

            for (int i = 0; i < table1.Columns.Count; i++)
            {
                if (table1.Columns[i].ColumnName != table2.Columns[i].ColumnName)
                    return false;
                // Could also check table1.Columns[i].DataType != table2.Columns[i].DataType
                // but for this use case, name and value comparison might be sufficient.
            }

            // Check row count
            if (table1.Rows.Count != table2.Rows.Count)
                return false;

            // Check cell values row by row, column by column
            for (int i = 0; i < table1.Rows.Count; i++)
            {
                for (int j = 0; j < table1.Columns.Count; j++)
                {
                    object val1 = table1.Rows[i][j];
                    object val2 = table2.Rows[i][j];

                    // Handle DBNull values
                    if (val1 == DBNull.Value && val2 == DBNull.Value)
                        continue;
                    if (val1 == DBNull.Value || val2 == DBNull.Value)
                        return false;

                    if (!val1.Equals(val2))
                        return false;
                }
            }

            return true;
        }
    }
}
