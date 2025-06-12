using System.Data;
using System.Text;

namespace OracleOptimizer.Services
{
    public static class DataTableComparator
    {
        public static bool AreIdentical(DataTable dt1, DataTable dt2, out string details)
        {
            StringBuilder sb = new StringBuilder();
            bool identical = true;

            if (dt1.Rows.Count != dt2.Rows.Count)
            {
                sb.AppendLine($"Row count mismatch: Before={dt1.Rows.Count}, After={dt2.Rows.Count}");
                identical = false;
            }

            if (dt1.Columns.Count != dt2.Columns.Count)
            {
                sb.AppendLine($"Column count mismatch: Before={dt1.Columns.Count}, After={dt2.Columns.Count}");
                identical = false;
            }

            if (!identical)
            {
                details = sb.ToString();
                return false;
            }

            for (int i = 0; i < dt1.Columns.Count; i++)
            {
                if (dt1.Columns[i].ColumnName != dt2.Columns[i].ColumnName)
                {
                    sb.AppendLine($"Column name mismatch at index {i}: Before='{dt1.Columns[i].ColumnName}', After='{dt2.Columns[i].ColumnName}'");
                    identical = false;
                }
            }

            if (!identical)
            {
                details = sb.ToString();
                return false;
            }

            for (int i = 0; i < dt1.Rows.Count; i++)
            {
                for (int j = 0; j < dt1.Columns.Count; j++)
                {
                    object val1 = dt1.Rows[i][j];
                    object val2 = dt2.Rows[i][j];

                    if (!Equals(val1, val2))
                    {
                        sb.AppendLine($"Data mismatch at Row {i}, Column '{dt1.Columns[j].ColumnName}': Before='{val1}', After='{val2}'");
                        identical = false;
                    }
                }
            }

            details = identical ? "Data is identical." : sb.ToString();
            return identical;
        }
    }
}