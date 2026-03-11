using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace TransportAnalyze
{
    public partial class Form1 : Form
    {
        private Graph graph;
        private Simulator simulator;

        public Form1()
        {
            InitializeComponent();

            pictureBox1.Paint += pictureBox1_Paint;   // 이벤트 연결
            timer1.Interval = 1000;  // 1초마다
            timer2.Interval = 10000; // 1분마다
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (simulator == null)
                return;

            simulator.Secondloop();     // 1초 진행
            pictureBox1.Invalidate();   // 다시 그리기 신호
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (simulator == null)
                return;

            simulator.Minuteloop();     // 1분 마다 진행
            pictureBox1.Invalidate();   // 다시 그리기 신호
        }
        private void button1_Click(object sender, EventArgs e)
        {
            graph = new Graph();
            simulator = new Simulator(graph);
            //simulator.GenerateRandomTest(10, 20); // 랜덤데이터
            simulator.GenerateGumiMap();

            for(int i = 0; i < graph.Nodes.Count; i++)
                graph.Nodes[i].InitProcessRate();

            MessageBox.Show($"구미 교통망 로드 완료\nNodes: {graph.Nodes.Count}, Roads: {graph.Roads.Count}\n(금오공대 <-> 구미역)");

            timer1.Start();
            timer2.Start();
            pictureBox1.Invalidate();
        }
        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (graph == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Pen roadPen = new Pen(Color.Black, 2);
            Brush carBrush = Brushes.Red;
            Color color;

            // 1) 도로 그리기
            foreach (Road road in graph.Roads)
            {
                PointF from = new PointF((float)road.From.X, (float)road.From.Y);
                PointF to = new PointF((float)road.To.X, (float)road.To.Y);

                Road reverseRoad = null;
                for (int i = 0; i < graph.Roads.Count; i++)
                {
                    if (graph.Roads[i].From == road.To && graph.Roads[i].To == road.From)
                        reverseRoad = graph.Roads[i];
                }
                if (reverseRoad == null)
                {
                    // 단방향 => 중앙에 화살표 1개
                    DrawArrow(g, roadPen, from, to);
                    DrawNumber(g, road, from, to);
                }
                else
                {
                    // 양방향 => 선을 두 개로 벌려서 그리기
                    float offset = 8f; // 도로 사이 간격

                    (PointF newFrom, PointF newTo) = Offset(from, to, +offset);  // A->B 방향 화살표
                    DrawArrow(g, roadPen, newFrom, newTo);
                    DrawCar(g, road, newFrom, newTo);
                    DrawNumber(g, road, newFrom, newTo);
                    (PointF newTo2, PointF newFrom2) = Offset(from, to, -offset);  // B->A 방향 화살표
                    DrawArrow(g, roadPen, newFrom2, newTo2);
                    DrawCar(g, reverseRoad, newFrom2, newTo2);
                }

            }
            // 2) 노드(교차로) 그리기
            foreach (CrossRoad node in graph.Nodes)
            {
                double c = node.Congestion; // 0..1
                if (c < 0.5) color = Color.FromArgb(200, 7, 227, 25);
                else if (0.5 <= c && c < 0.8) color = Color.FromArgb(200, 241, 227, 39);
                else color = Color.FromArgb(200, 255, 0, 0);

                Brush nodeBrush = new SolidBrush(color);

                int r = 20;
                g.FillEllipse(nodeBrush, (int)node.X - r, (int)node.Y - r, r * 2, r * 2);

                // 교차로 내부에 대기중인 차량 숫자 표시
                int waitingCars = node.PriorityQueue.Count();
                if (waitingCars > 0)
                {
                    using (Font drawFont = new Font("Arial", 8))
                    using (SolidBrush drawBrush = new SolidBrush(Color.White))
                    {
                        g.DrawString(
                            waitingCars.ToString(),
                            drawFont,
                            drawBrush,
                            (float)node.X, (float)node.Y);
                    }
                }
            }
            DrawLegend(g);
        }
        private (PointF, PointF) Offset(PointF from, PointF to, float offset)
        {
            // 벡터 구하기
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001f) return default;

            dx /= len;
            dy /= len;

            // 평행 이동 방향: (-dy, dx)
            float ox = -dy * offset;
            float oy = dx * offset;

            PointF a = new PointF(from.X + ox, from.Y + oy);
            PointF b = new PointF(to.X + ox, to.Y + oy);

            return (a, b);

        }
        private void DrawArrow(Graphics g, Pen pen, PointF a, PointF b, float arrowSize = 8)
        {
            g.DrawLine(pen, a, b);

            float vx = b.X - a.X;
            float vy = b.Y - a.Y;
            float len = (float)Math.Sqrt(vx * vx + vy * vy);
            if (len < 0.0001f) return;
            vx /= len; vy /= len;

            PointF left = new PointF(
                b.X - vx * arrowSize - vy * arrowSize * 0.5f,
                b.Y - vy * arrowSize + vx * arrowSize * 0.5f);

            PointF right = new PointF(
                b.X - vx * arrowSize + vy * arrowSize * 0.5f,
                b.Y - vy * arrowSize - vx * arrowSize * 0.5f);

            g.DrawLine(pen, b, left);
            g.DrawLine(pen, b, right);
        }
        private void DrawCar(Graphics g, Road road,PointF from, PointF to)
        {
            foreach (var car in road.CarQueue)
            {
                double dist = car.MeanSpeed * car.VisitTime * 10;

                double t = dist / road.Length;
                if (t < 0) t = 0;
                if (t > 1) t = 1;

                int x = (int)(from.X + (to.X - from.X) * t);
                int y = (int)(from.Y + (to.Y - from.Y) * t);
                if (car.IsArrived == true) g.FillRectangle(Brushes.White, x, y, 4, 4);
                else g.FillRectangle(Brushes.Red, x, y, 4, 4);
            }
        }
        private void DrawNumber(Graphics g, Road road, PointF from, PointF to)
        {
            int roadCars = road.CarQueue.Count;
            double congestion = road.CongestionSum;

            if (roadCars > 0)
            {
                // 도로 중간 좌표 계산
                float midX = (from.X + to.X) / 2f;
                float midY = (from.Y + to.Y) / 2f;

                using (Font f = new Font("Arial", 8))
                using (Brush b = new SolidBrush(Color.Black))
                {
                    g.DrawString($"{roadCars}", f, b, midX, midY);
                    //g.DrawString($"{congestion}", f, b, midX-15, midY);
                }
            }
        }
        private void DrawLegend(Graphics g)
        {
            // 1. 범례 박스 설정
            int boxX = 10;
            int boxY = 10;
            int boxWidth = 160;
            int boxHeight = 110;

            // 배경을 반투명 흰색으로 칠해 글씨가 잘 보이게 함
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.FillRectangle(bgBrush, boxX, boxY, boxWidth, boxHeight);
            }
            // 테두리
            g.DrawRectangle(Pens.Black, boxX, boxY, boxWidth, boxHeight);

            // 2. 제목 그리기 ("실시간 교통망")
            using (Font titleFont = new Font("맑은 고딕", 11, FontStyle.Bold))
            {
                g.DrawString("실시간 교통망", titleFont, Brushes.Black, boxX + 10, boxY + 10);
            }

            // 3. 혼잡도 범례 그리기
            using (Font textFont = new Font("맑은 고딕", 9))
            {
                int startX = boxX + 15;
                int startY = boxY + 40;
                int gap = 20; // 줄 간격
                int circleSize = 10;

                // 색상 정의 (기존 로직과 동일)
                Color colorGreen = Color.FromArgb(200, 7, 227, 25);
                Color colorYellow = Color.FromArgb(200, 241, 227, 39);
                Color colorRed = Color.FromArgb(200, 255, 0, 0);

                // (1) 원활
                using (Brush b = new SolidBrush(colorGreen))
                {
                    g.FillEllipse(b, startX, startY, circleSize, circleSize);
                    g.DrawString("원활 (혼잡도 < 0.5)", textFont, Brushes.Black, startX + 15, startY - 2);
                }

                // (2) 서행
                using (Brush b = new SolidBrush(colorYellow))
                {
                    g.FillEllipse(b, startX, startY + gap, circleSize, circleSize);
                    g.DrawString("서행 (0.5 ~ 0.8)", textFont, Brushes.Black, startX + 15, startY + gap - 2);
                }

                // (3) 정체
                using (Brush b = new SolidBrush(colorRed))
                {
                    g.FillEllipse(b, startX, startY + gap * 2, circleSize, circleSize);
                    g.DrawString("정체 (혼잡도 ≥ 0.8)", textFont, Brushes.Black, startX + 15, startY + gap * 2 - 2);
                }
            }
        }
    }
}