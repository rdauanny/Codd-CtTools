using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CoddCtTools
{
    /// <summary>
    /// Popup estilo Excel para filtro por coluna no DataGridView.
    /// Suporta digitação para filtrar por "contém" na coluna.
    /// </summary>
    internal static class ColumnFilterPopup
    {
        private const string AllLabel = "(Todos)";
        private const int MaxHeight = 300;
        private const int Width = 250;
        private const int TextBoxHeight = 26;

        /// <summary>
        /// Mostra o popup de filtro para a coluna e retorna o valor do filtro.
        /// Retorna null para "Todos" (limpar filtro) ou o texto para filtro "contém".
        /// </summary>
        public static string? Show(DataGridView grid, DataGridViewColumn column, string? currentFilter)
        {
            if (grid == null || column == null) return null;

            var uniqueValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var val = row.Cells[column.Index]?.Value?.ToString() ?? "";
                uniqueValues.Add(string.IsNullOrEmpty(val) ? "" : val);
            }

            var items = new List<string> { AllLabel };
            items.AddRange(uniqueValues.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

            var txtFilter = new TextBox
            {
                Font = SystemFonts.DefaultFont,
                Location = new Point(4, 4),
                Size = new Size(Width - 12, TextBoxHeight - 4),
                PlaceholderText = "Digite para filtrar por contém...",
                Text = currentFilter ?? ""
            };

            var listBox = new ListBox
            {
                Font = SystemFonts.DefaultFont,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                ItemHeight = 20,
                DrawMode = DrawMode.OwnerDrawFixed,
                Location = new Point(4, TextBoxHeight + 4),
                Width = Width - 12
            };
            listBox.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Index >= 0 && e.Index < items.Count)
                {
                    var text = items[e.Index];
                    var font = text == AllLabel ? new Font(e.Font!, FontStyle.Bold) : e.Font!;
                    e.Graphics!.DrawString(text, font, Brushes.Black, e.Bounds);
                    if (text == AllLabel && font != e.Font) font.Dispose();
                }
                e.DrawFocusRectangle();
            };

            // Filtrar a lista conforme o usuário digita
            var allItems = new List<string>(items);
            void RefreshList()
            {
                var search = txtFilter.Text.Trim().ToLowerInvariant();
                listBox.Items.Clear();
                if (string.IsNullOrEmpty(search))
                {
                    foreach (var item in allItems)
                        listBox.Items.Add(item);
                }
                else
                {
                    foreach (var item in allItems.Where(x =>
                        x == AllLabel || (x?.ToLowerInvariant().Contains(search) ?? false)))
                        listBox.Items.Add(item);
                }
            }

            foreach (var item in items)
                listBox.Items.Add(item);
            txtFilter.TextChanged += (s, e) => RefreshList();

            var itemHeight = 20;
            var count = Math.Min(items.Count, 12);
            listBox.Height = Math.Min(count * itemHeight + 4, MaxHeight);
            var formHeight = TextBoxHeight + listBox.Height + 10;

            using var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                BackColor = SystemColors.Window,
                Size = new Size(Width, formHeight),
                StartPosition = FormStartPosition.Manual,
                Padding = new Padding(2)
            };

            var headerRect = grid.GetCellDisplayRectangle(column.Index, -1, true);
            var pt = grid.PointToScreen(new Point(headerRect.Left, headerRect.Bottom));
            form.Location = new Point(pt.X, pt.Y);

            form.Controls.Add(txtFilter);
            form.Controls.Add(listBox);

            string? result = null;
            var applied = false;

            void ApplyFilter()
            {
                var text = txtFilter.Text.Trim();
                applied = true;
                result = string.IsNullOrEmpty(text) ? null : text;
                form.Close();
            }

            void ApplyFromList()
            {
                if (listBox.SelectedIndex < 0) return;
                var sel = listBox.SelectedItem?.ToString();
                if (sel == AllLabel)
                {
                    applied = true;
                    result = null;
                }
                else if (!string.IsNullOrEmpty(sel))
                {
                    applied = true;
                    result = sel;
                }
                form.Close();
            }

            txtFilter.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ApplyFilter(); e.Handled = true; }
                if (e.KeyCode == Keys.Escape) { form.Close(); e.Handled = true; }
                if (e.KeyCode == Keys.Down && listBox.Items.Count > 0) { listBox.Focus(); e.Handled = true; }
            };

            listBox.DoubleClick += (s, e) => ApplyFromList();
            listBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { ApplyFromList(); e.Handled = true; }
                if (e.KeyCode == Keys.Escape) { form.Close(); e.Handled = true; }
                if (e.KeyCode == Keys.Up && listBox.SelectedIndex <= 0) { txtFilter.Focus(); e.Handled = true; }
            };

            if (!string.IsNullOrEmpty(currentFilter))
            {
                txtFilter.Text = currentFilter;
                RefreshList();
                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    if (string.Equals(listBox.Items[i]?.ToString(), currentFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        listBox.SelectedIndex = i;
                        break;
                    }
                }
                if (listBox.SelectedIndex < 0)
                    listBox.SelectedIndex = 0;
            }
            else
            {
                listBox.SelectedIndex = 0;
            }

            form.Deactivate += (s, e) => form.Close();
            form.Shown += (s, e) => txtFilter.Focus();
            form.ShowDialog(grid);

            return applied ? result : currentFilter;
        }
    }
}
