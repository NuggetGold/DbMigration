using System.Drawing;
using System.Windows.Forms;

namespace DbMigration.App.Controls;

public sealed class ThemedDataGridView : DataGridView
{
    public ThemedDataGridView()
    {
        DoubleBuffered = true;
        BackgroundColor = OpencodeTheme.BackgroundPanel;
        BorderStyle = BorderStyle.None;
        GridColor = OpencodeTheme.BorderSubtle;
        EnableHeadersVisualStyles = false;
        RowHeadersVisible = false;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        AllowUserToOrderColumns = false;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 32;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        SelectionMode = DataGridViewSelectionMode.CellSelect;
        MultiSelect = false;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        ReadOnly = true;
        Font = UiFonts.MonoSmall;
        ShowCellToolTips = true;

        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = OpencodeTheme.BackgroundPanel,
            ForeColor = OpencodeTheme.Text,
            SelectionBackColor = OpencodeTheme.WithAlpha(OpencodeTheme.Accent, 100),
            SelectionForeColor = OpencodeTheme.Text,
            Font = UiFonts.MonoSmall,
            Padding = new Padding(6, 0, 6, 0),
        };

        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = OpencodeTheme.BackgroundElement,
            ForeColor = OpencodeTheme.Primary,
            SelectionBackColor = OpencodeTheme.BackgroundElement,
            SelectionForeColor = OpencodeTheme.Primary,
            Font = UiFonts.BodySmall,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0),
        };

        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = OpencodeTheme.WithAlpha(OpencodeTheme.BackgroundElement, 120),
            ForeColor = OpencodeTheme.Text,
            SelectionBackColor = OpencodeTheme.WithAlpha(OpencodeTheme.Accent, 100),
            SelectionForeColor = OpencodeTheme.Text,
        };

        RowTemplate.Height = 26;
    }

    public void LoadResult(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        SuspendLayout();
        try
        {
            Columns.Clear();
            Rows.Clear();
            foreach (string column in result.Columns)
            {
                Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = column,
                    Name = column,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    MinimumWidth = 70,
                    Width = 140,
                });
            }

            foreach (object?[] row in result.Rows)
            {
                object[] values = new object[row.Length];
                for (int i = 0; i < row.Length; i++)
                {
                    values[i] = row[i] ?? (object)DBNull.Value;
                }

                Rows.Add(values);
            }
        }
        finally
        {
            ResumeLayout();
        }
    }
}
