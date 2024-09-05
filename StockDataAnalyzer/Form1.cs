using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;

namespace StockDataAnalyzer
{
    public partial class Form1 : Form
    {
        private DataTable stockData = new DataTable();
        private Chart stockChart = new Chart();
        private TextBox txtPredictions = new TextBox();
        private TabControl tabControl = new TabControl();
        private CheckBox chkSMA = new CheckBox();
        private CheckBox chkEMA = new CheckBox();
        private int predictionWindow = 5;
        private string apiKey = "I8PMPRKJ4Q6I2EZS";
        private string symbol = "AAPL";

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            RunPythonScript(); // Run the Python script to train the model
            Task.Run(async () => await LoadStockData()); // Fetch data asynchronously
        }

        private void InitializeUI()
        {
            // Set the form to use a dark theme
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;

            // Initialize TabControl
            tabControl.Dock = DockStyle.Fill;
            tabControl.BackColor = Color.FromArgb(45, 45, 48);
            tabControl.ForeColor = Color.White;
            this.Controls.Add(tabControl);

            // Initialize Chart Tab
            TabPage tabPageChart = new TabPage("Chart");
            tabPageChart.BackColor = Color.FromArgb(45, 45, 48);
            InitializeChart();
            tabPageChart.Controls.Add(stockChart);

            // Initialize SMA and EMA Checkboxes
            InitializeCheckbox(chkSMA, "Show SMA", UpdateSMAVisibility);
            InitializeCheckbox(chkEMA, "Show EMA", UpdateEMAVisibility);

            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            panel.Controls.Add(chkSMA);
            panel.Controls.Add(chkEMA);
            tabPageChart.Controls.Add(panel);

            tabControl.TabPages.Add(tabPageChart);

            // Initialize Metrics and Predictions Tab
            TabPage tabPageMetrics = new TabPage("Metrics and Predictions");
            tabPageMetrics.BackColor = Color.FromArgb(45, 45, 48);
            InitializeTextBox(txtPredictions);
            tabPageMetrics.Controls.Add(txtPredictions);
            tabControl.TabPages.Add(tabPageMetrics);
        }

        private void InitializeCheckbox(CheckBox checkBox, string text, Action eventHandler)
        {
            checkBox.Text = text;
            checkBox.ForeColor = Color.White;
            checkBox.BackColor = Color.FromArgb(45, 45, 48);
            checkBox.CheckedChanged += new EventHandler((s, e) => eventHandler());
        }

        private void InitializeTextBox(TextBox textBox)
        {
            textBox.Multiline = true;
            textBox.Dock = DockStyle.Fill;
            textBox.BackColor = Color.FromArgb(30, 30, 30);
            textBox.ForeColor = Color.White;
            textBox.Font = new Font("Segoe UI", 12);
        }

        private void InitializeChart()
        {
            stockChart.Dock = DockStyle.Fill;
            stockChart.BackColor = Color.FromArgb(45, 45, 48);
            stockChart.ForeColor = Color.White;

            ChartArea chartArea = new ChartArea("MainArea")
            {
                AxisX = {
                    Title = "Date",
                    IntervalType = DateTimeIntervalType.Days,
                    LabelStyle = { ForeColor = Color.White },
                    LineColor = Color.White,
                    MajorGrid = { LineColor = Color.Gray },
                    ScaleView = { Zoomable = true },
                },
                AxisY = {
                    Title = "Price",
                    LabelStyle = { ForeColor = Color.White },
                    LineColor = Color.White,
                    MajorGrid = { LineColor = Color.Gray }
                },
                BackColor = Color.FromArgb(45, 45, 48),
                CursorX = { IsUserEnabled = true, IsUserSelectionEnabled = true },
                CursorY = { IsUserEnabled = true, IsUserSelectionEnabled = true }
            };

            stockChart.ChartAreas.Add(chartArea);
            stockChart.MouseWheel += StockChart_MouseWheel;

            // Add a legend to the chart
            stockChart.Legends.Add(new Legend("Legend")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Center,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            });
        }

        private void StockChart_MouseWheel(object? sender, MouseEventArgs e)
        {
            try
            {
                var chart = sender as Chart;
                if (chart == null) return;

                var xAxis = chart.ChartAreas[0].AxisX;
                var yAxis = chart.ChartAreas[0].AxisY;

                if (e.Delta < 0)
                {
                    xAxis.ScaleView.ZoomReset();
                    yAxis.ScaleView.ZoomReset();
                }
                else if (e.Delta > 0)
                {
                    double xMin = xAxis.ScaleView.ViewMinimum;
                    double xMax = xAxis.ScaleView.ViewMaximum;
                    double posXStart = xAxis.PixelPositionToValue(e.Location.X) - (xMax - xMin) / 4;
                    double posXFinish = xAxis.PixelPositionToValue(e.Location.X) + (xMax - xMin) / 4;
                    xAxis.ScaleView.Zoom(posXStart, posXFinish);

                    double yMin = yAxis.ScaleView.ViewMinimum;
                    double yMax = yAxis.ScaleView.ViewMaximum;
                    double posYStart = yAxis.PixelPositionToValue(e.Location.Y) - (yMax - yMin) / 4;
                    double posYFinish = yAxis.PixelPositionToValue(e.Location.Y) + (yMax - yMin) / 4;
                    yAxis.ScaleView.Zoom(posYStart, posYFinish);
                }
            }
            catch { /* Handle exceptions if necessary */ }
        }

        private async Task LoadStockData()
        {
            try
            {
                stockData.Clear();
                string url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={symbol}&apikey={apiKey}&datatype=json";

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    HttpResponseMessage response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Failed to fetch data from API. Status code: {response.StatusCode}");
                        return;
                    }

                    string data = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(data))
                    {
                        MessageBox.Show("No data received from API.");
                        return;
                    }

                    JObject json = JObject.Parse(data);
                    var timeSeries = json["Time Series (Daily)"];
                    if (timeSeries == null)
                    {
                        MessageBox.Show("Error fetching data: Invalid response format from API.");
                        return;
                    }

                    stockData.Columns.Add("Date");
                    stockData.Columns.Add("Close");

                    foreach (var item in timeSeries.Children<JProperty>())
                    {
                        string dateStr = item.Name;
                        var closeStr = item.Value["4. close"]?.ToString();
                        if (DateTime.TryParse(dateStr, out var date) && double.TryParse(closeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var close))
                        {
                            stockData.Rows.Add(date, close);
                        }
                    }

                    if (stockData.Rows.Count > 0)
                    {
                        Invoke((MethodInvoker)DisplayStockData);
                    }
                    else
                    {
                        MessageBox.Show("Parsed data is empty.");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("The request took too long to complete and was canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading data: " + ex.Message);
            }
        }

        private void DisplayStockData()
        {
            if (stockData.Rows.Count == 0) return;

            var dates = stockData.AsEnumerable()
                .Select(row => DateTime.TryParse(row["Date"]?.ToString(), out DateTime date) ? date : DateTime.MinValue)
                .Where(d => d != DateTime.MinValue)
                .ToArray();

            var prices = stockData.AsEnumerable()
                .Select(row => double.TryParse(row["Close"]?.ToString(), out double price) ? price : (double?)null)
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToArray();

            if (dates.Length > predictionWindow && prices.Length > predictionWindow)
            {
                // Ridge Regression
                double[] X = Enumerable.Range(0, prices.Length).Select(x => (double)x).ToArray();
                double X_mean = X.Average();
                double Y_mean = prices.Average();
                double lambda = 1; // Regularization parameter
                double numerator = X.Zip(prices, (x, y) => (x - X_mean) * (y - Y_mean)).Sum();
                double denominator = X.Sum(x => Math.Pow(x - X_mean, 2)) + lambda;
                double slope = numerator / denominator;
                double intercept = Y_mean - slope * X_mean;

                var prediction = slope * X.Length + intercept;

                // Calculate MAE and RMSE for Ridge Regression
                double maeRidge = prices.Select((t, i) => Math.Abs(t - (slope * i + intercept))).Average();
                double rmseRidge = Math.Sqrt(prices.Select((t, i) => Math.Pow(t - (slope * i + intercept), 2)).Average());

                // SMA and EMA Calculation
                var sma = CalculateSMA(prices, 10);
                var ema = CalculateEMA(prices, 10);

                // Calculate MAE and RMSE for SMA
                double maeSMA = CalculateMAE(prices.Skip(9).ToArray(), sma);
                double rmseSMA = CalculateRMSE(prices.Skip(9).ToArray(), sma);

                // Calculate MAE and RMSE for EMA
                double maeEMA = CalculateMAE(prices, ema);
                double rmseEMA = CalculateRMSE(prices, ema);

                // Display the prediction and errors
                txtPredictions.Text = $"Predicted next stock price for {symbol} using ridge regression is ${prediction:F2}.\r\n";
                txtPredictions.Text += $"The Mean Absolute Error (MAE) for ridge regression is {maeRidge:F4}.\r\n";
                txtPredictions.Text += $"The Root Mean Squared Error (RMSE) for ridge regression is {rmseRidge:F4}.\r\n";
                txtPredictions.Text += $"\r\nSMA Analysis:\r\n";
                txtPredictions.Text += $"The Mean Absolute Error (MAE) for SMA is {maeSMA:F4}.\r\n";
                txtPredictions.Text += $"The Root Mean Squared Error (RMSE) for SMA is {rmseSMA:F4}.\r\n";
                txtPredictions.Text += $"\r\nEMA Analysis:\r\n";
                txtPredictions.Text += $"The Mean Absolute Error (MAE) for EMA is {maeEMA:F4}.\r\n";
                txtPredictions.Text += $"The Root Mean Squared Error (RMSE) for EMA is {rmseEMA:F4}.\r\n";

                // Populate the chart with actual and predicted data
                AddSeriesToChart(dates, prices, sma, ema);
            }
            else
            {
                MessageBox.Show("No valid data available to display.");
            }
        }

        private void AddSeriesToChart(DateTime[] dates, double[] prices, double[] sma, double[] ema)
        {
            var seriesStock = new Series("Stock Price")
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime,
                BorderWidth = 2,
                Color = Color.Cyan
            };
            seriesStock.Points.DataBindXY(dates, prices);

            var seriesSMA = new Series("SMA")
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime,
                BorderWidth = 2,
                Color = Color.Red,
                IsVisibleInLegend = false
            };
            seriesSMA.Points.DataBindXY(dates.Skip(9).ToArray(), sma);

            var seriesEMA = new Series("EMA")
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime,
                BorderWidth = 2,
                Color = Color.Blue,
                IsVisibleInLegend = false
            };
            seriesEMA.Points.DataBindXY(dates, ema);

            stockChart.Series.Clear();
            stockChart.Series.Add(seriesStock);
            stockChart.Series.Add(seriesSMA);
            stockChart.Series.Add(seriesEMA);
        }

        private void RunPythonScript()
        {
            try
            {
                string pythonPath = "python"; // Path to Python executable
                string scriptPath = "train_model.py"; // Path to Python script

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(start))
                {
                    if (process == null) throw new Exception("Failed to start Python process.");

                    string result = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(result))
                        Console.WriteLine(result);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"Error: {error}");
                        MessageBox.Show($"Error running Python script: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while running the Python script: {ex.Message}");
            }
        }

        private double[] CalculateSMA(double[] prices, int period)
        {
            var sma = new double[prices.Length - period + 1];
            for (int i = 0; i < sma.Length; i++)
            {
                sma[i] = prices.Skip(i).Take(period).Average();
            }
            return sma;
        }

        private double[] CalculateEMA(double[] prices, int period)
        {
            double[] ema = new double[prices.Length];
            double multiplier = 2.0 / (period + 1);
            ema[0] = prices[0];

            for (int i = 1; i < prices.Length; i++)
            {
                ema[i] = (prices[i] - ema[i - 1]) * multiplier + ema[i - 1];
            }
            return ema;
        }

        private double CalculateMAE(double[] actual, double[] predicted)
        {
            return actual.Zip(predicted, (a, p) => Math.Abs(a - p)).Average();
        }

        private double CalculateRMSE(double[] actual, double[] predicted)
        {
            return Math.Sqrt(actual.Zip(predicted, (a, p) => Math.Pow(a - p, 2)).Average());
        }

        private void UpdateSMAVisibility()
        {
            stockChart.Series["SMA"].IsVisibleInLegend = chkSMA.Checked;
            stockChart.Series["SMA"].Enabled = chkSMA.Checked;
        }

        private void UpdateEMAVisibility()
        {
            stockChart.Series["EMA"].IsVisibleInLegend = chkEMA.Checked;
            stockChart.Series["EMA"].Enabled = chkEMA.Checked;
        }
    }

    public class LoadingForm : Form
    {
        public LoadingForm()
        {
            Text = "Loading";
            Size = new Size(200, 100);
            StartPosition = FormStartPosition.CenterScreen;
            Label label = new Label()
            {
                Text = "Fetching data, please wait...",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(label);
        }
    }
}
