using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TransportAnalyze
{
    class Simulator
    {
        public Graph Gp { get; set; }   
        private Random _rd;

        public Simulator(Graph gp = null)
        {
            Gp = gp;
            _rd = new Random();

            foreach (CrossRoad node in gp.Nodes)
            {
                node.InitProcessRate();
            }
        }
        public Car CreateCar()
        {
            double meanSpeed = (_rd.NextDouble() * (30.0 - 20.0) + 20.0); // 시속
            double urgency = _rd.NextDouble();
            int index = _rd.Next(Gp.Nodes.Count);
            CrossRoad goal = Gp.Nodes[index];

            Car car = new Car(meanSpeed, urgency, goal);
            return car;
        }
        public void TickTime()
        {
            foreach (var road in Gp.Roads)
            {
                foreach (var car in road.CarQueue) car.VisitTime++;
            }
            foreach (var node in Gp.Nodes)
            {
                foreach (var car in node.CarQueue) car.VisitTime++;
            }
        }
        public void Secondloop()
        {
            TickTime();
            int createCount = _rd.Next(2) + 10;
            for (int i = 0; i< createCount; i++)
            {
                Car car = CreateCar();
                Gp.Entrys[_rd.Next(Gp.Entrys.Count)].CarQueue.Enqueue(car);
            }
            for (int i = 0; i < Gp.Roads.Count; i++)
            {
                Gp.Roads[i].ServiceCars();
                //Gp.Roads[i].CongestionSum += Gp.Roads[i].CarQueue.Count / Gp.Roads[i].Length;
            }
            for (int i = 0; i < Gp.Nodes.Count; i++)
            {
                Gp.Nodes[i].CheckPriority();
                Gp.Nodes[i].ServiceCar();
            }
        }
        public void Minuteloop()
        {
            foreach (Road road in Gp.Roads)
            {
                road.SetServiceRate(_rd.NextDouble());
                road.Congestion = road.CongestionSum/60;
                road.CongestionSum = 0;
            }
            foreach(CrossRoad node in Gp.Nodes)
            {
                double capacityPerMin = node.SecondProcess * 60.0; 
                double arrivals = node.ArrivalCount;
                double C_raw = capacityPerMin > 0 ? arrivals / capacityPerMin : 1.0;
                node.EMA_DemandRatio = CrossRoad.EMA_ALPHA * C_raw + (1 - CrossRoad.EMA_ALPHA) * node.EMA_DemandRatio;
                node.Congestion = Math.Min(1.0, node.EMA_DemandRatio);
                node.ArrivalCount = 0;
            }
        }
        public void GenerateGumiMap()
        {
            double pixelSize = 7.33;
            // 맵 좌표 범위 (약 850x500 캔버스 기준)

            // 1. 교차로(노드) 생성 (ID, X, Y)
            var nKumoh = Gp.AddCrossRoad(1, 750, 100);  // 금오공대
            var nYangho = Gp.AddCrossRoad(2, 650, 150);  // 양호사거리 (3개 연결)
            var nIndong = Gp.AddCrossRoad(3, 850, 250);  // 인동사거리 (4개 연결, 동쪽)
            var nSanhoBridge = Gp.AddCrossRoad(4, 450, 250);  // 산호대교 서단 (4개 연결, 중앙)
            var nExportTower = Gp.AddCrossRoad(5, 350, 300);  // 수출탑 오거리 (4개 연결)
            var nTerminal = Gp.AddCrossRoad(6, 150, 350);  // 버스터미널 (4개 연결)
            var nStation = Gp.AddCrossRoad(7, 50, 400);   // 구미역 (시내, 목적지)
            var nNamTong = Gp.AddCrossRoad(8, 250, 450);  // 남통동 (남쪽 진입로)
            var nOkgye = Gp.AddCrossRoad(9, 800, 50);   // 옥계동 (북동쪽 진입로)
            var nSinpyung = Gp.AddCrossRoad(10, 550, 300); // 신평동 (공단 외곽, 3개 연결)

            // 2. 도로 연결 (From, To, Length, Lanes, TrafficLights, IsEntry)
            // TrafficLights: 신호등 개수 (가중치에 반영되는 요소)

            // A. 동부 외곽/진입로
            // 옥계 <-> 금오공대 (ENTRY)
            Gp.AddRoad(nOkgye, nKumoh, 100 * pixelSize, 2, 0, true);
            Gp.AddRoad(nKumoh, nOkgye, 100 * pixelSize, 2, 0, false);

            // 금오공대 <-> 양호사거리 (ENTRY)
            Gp.AddRoad(nKumoh, nYangho, 120 * pixelSize, 3, 1, true);
            Gp.AddRoad(nYangho, nKumoh, 120 * pixelSize, 3, 0, false);

            // 양호사거리 <-> 인동사거리 (복잡한 외곽 순환)
            Gp.AddRoad(nYangho, nIndong, 200 * pixelSize, 3, 2, false);
            Gp.AddRoad(nIndong, nYangho, 200 * pixelSize, 3, 2, false);

            // B. 다리(산호대교) 구간
            // 양호사거리 <-> 산호대교 (매우 긴 다리, 신호 거의 없음)
            Gp.AddRoad(nYangho, nSanhoBridge, 300 * pixelSize, 4, 0, false);
            Gp.AddRoad(nSanhoBridge, nYangho, 300 * pixelSize, 4, 0, false);

            // C. 중심부 순환 (산호대교 서단 교차로)
            // 산호대교 <-> 수출탑
            Gp.AddRoad(nSanhoBridge, nExportTower, 150 * pixelSize, 4, 2, false);
            Gp.AddRoad(nExportTower, nSanhoBridge, 150 * pixelSize, 4, 2, false);

            // 산호대교 <-> 신평동 (공단 우회)
            Gp.AddRoad(nSanhoBridge, nSinpyung, 100 * pixelSize, 3, 1, false);
            Gp.AddRoad(nSinpyung, nSanhoBridge, 100 * pixelSize, 3, 1, false);

            // D. 공단 및 시내 내부
            // 수출탑 <-> 공단/터미널 (직선)
            Gp.AddRoad(nExportTower, nTerminal, 200 * pixelSize, 3, 3, false);
            Gp.AddRoad(nTerminal, nExportTower, 200 * pixelSize, 3, 3, false);

            // 수출탑 <-> 남통동 (남부 연결)
            Gp.AddRoad(nExportTower, nNamTong, 150 * pixelSize, 3, 2, false);
            Gp.AddRoad(nNamTong, nExportTower, 150 * pixelSize, 3, 2, false);

            // 남통동 <-> 버스터미널
            Gp.AddRoad(nNamTong, nTerminal, 100 * pixelSize, 3, 2, false);
            Gp.AddRoad(nTerminal, nNamTong, 100 * pixelSize, 3, 2, false);

            // E. 구미역(시내) 진입/진출 (혼잡 구간)
            // 버스터미널 <-> 구미역 (시내, 2차선)
            Gp.AddRoad(nTerminal, nStation, 120 * pixelSize, 2, 4, false);
            Gp.AddRoad(nStation, nTerminal, 120 * pixelSize, 2, 3, true); // ENTRY: 역에서 나오는 차

            // 신평동 <-> 구미역 (시내 우회로)
            Gp.AddRoad(nSinpyung, nStation, 250 * pixelSize, 3, 3, false);
            Gp.AddRoad(nStation, nSinpyung, 250 * pixelSize, 3, 3, false);

            // F. 추가 진입로 (남통동 ENTRY)
            // 남통동에서 외부에서 들어오는 차량 ENTRY
            Gp.AddRoad(nNamTong, nTerminal, 50 * pixelSize, 2, 1, true); // 임시 ENTRY 도로 (단방향)
        }
    }
}