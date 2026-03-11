using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TransportAnalyze
{
    class PriorityQueue<T, K> where K : IComparable<K> // 우선순위 값 비교를 위한 IComparable 인터페이스 구현
    {
        private int _heapBase; //최소힙이면 -1 최대 힙이면 1
        private List<(T item, K priority)> _elements; //리스트 각 요소는 튜플
        public PriorityQueue(int heapBase)
        {
            _heapBase = heapBase;
            _elements = new List<(T, K)>(3000);
            _elements.Add(default); // 0번은 사용 안 함
        }
        private bool Compare(K a, K b)
        {
            int cmp = a.CompareTo(b);
            return _heapBase == 1 ? (cmp >= 0) : (cmp <= 0);
        }
        public void Enqueue(T item, K priority)
        {
            var pair = (item, priority); //리스트에 들어갈 튜플
            _elements.Add(pair);
            int index = _elements.Count - 1;

            while (index > 1)
            {
                int parent = index / 2;
                if (Compare(_elements[parent].priority, priority)) break;

                _elements[index] = _elements[parent];
                index = parent;
            }
            _elements[index] = pair;
        }
        public T Dequeue()
        {
            if (_elements.Count <= 1) return default;

            T result = _elements[1].item;
            var last = _elements[_elements.Count - 1];
            _elements.RemoveAt(_elements.Count - 1);

            if (_elements.Count == 1) return result; //데이터가 1개뿐이었다면 바로 리턴
            int index = 1;

            while (true)
            {
                int left = index * 2;
                int right = left + 1;

                if (left >= _elements.Count) break;

                int child = left;
                //최소,최대힙 고려하여 우선순위에 있는 자식을 선택
                if (right < _elements.Count && Compare(_elements[right].priority, _elements[left].priority))
                    child = right;

                if (Compare(last.priority, _elements[child].priority)) break;

                _elements[index] = _elements[child];
                index = child;
            }
            _elements[index] = last;
            return result;
        }
        public int Count() { return _elements.Count - 1; }
    }
    class Car
    {
        public double MeanSpeed { get; set; }
        public int VisitTime { get; set; }  // 도로/교차로 체류시간
        public double Urgency { get; private set; } //차량 긴급도
        public bool IsArrived { get; set; }

        public CrossRoad Goal { get; set; }

        public Car(double meanSpeed, double urgency, CrossRoad goal)
        {
            MeanSpeed = meanSpeed;
            Urgency = urgency;
            Goal = goal;
            VisitTime = 0;
            IsArrived = false;
        }
    }
    class Road
    {
        // 출발/도착 교차로
        public CrossRoad From { get; private set; }
        public CrossRoad To { get; private set; }

        // 도로 특성
        public double Length { get; set; }
        public int Lanes { get; set; }       // 차선 수
        public int TrafficLight { get; set; } // 신호등 수
        public double CongestionSum { get; set; }
        public double Congestion { get; set; } // 정체도
        public bool IsEntry { get; set; } //진입 도로이면 1, 아니면 0

        private double _serviceRate = 1;  // μ (서비스율)

        // 차량 대기열
        public Queue<Car> CarQueue { get; } = new Queue<Car>();

        //private Random _rd;

        public Road(CrossRoad from, CrossRoad to, double length, int lanes, int trafficLight, bool isEntry)
        {
            From = from;
            To = to;
            Length = length;
            Lanes = lanes;
            TrafficLight = trafficLight;
            IsEntry = isEntry;
            SetServiceRate();
            //_rd = new Random();
            CongestionSum = 0;
            Congestion = 0;
        }
        public double GetGcost() { return CarQueue.Count / Length * To.CarQueue.Count * (1/_serviceRate); }
        public void SetServiceRate(double accidentImpact = 0)
        {
            double baseRate = (Lanes / (Lanes + 1.0)) * (1.0 / (TrafficLight + 1.0));
            //1.0은 감쇠상수 (차선과 신호등 수에 의한 서비스율의 급격한 변화 방지)
            _serviceRate = baseRate * (1 - 0.5 * accidentImpact);
        }
        public void ServiceCars()
        {
            while (CarQueue.Count > 0)
            {
                Car car = CarQueue.Peek();
                double distance = car.MeanSpeed * car.VisitTime * 10;
                if (distance < Length) break;
                //if (_rd.NextDouble() >= _serviceRate) break;

                car.VisitTime = 0;
                To.OnArrival(CarQueue.Dequeue());

            }
        }
    }
    class CrossRoad
    {
        public int Id { get; private set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double ArrivalCount { get; set; }
        public double EMA_DemandRatio { get; set; }
        public double Congestion { get; set; }
        public double SecondProcess { get; set; }

        private double _maxProcess;
        public const double EMA_ALPHA = 0.25;

        // 연결된 도로 목록
        public List<Road> IncomingRoads { get; } = new List<Road>();
        public List<Road> OutgoingRoads { get; } = new List<Road>();
        public Queue<Car> CarQueue { get; } = new Queue<Car>();

        // 교차로 우선순위 큐 (긴급도 + 대기시간)
        public PriorityQueue<Car, double> PriorityQueue { get; } = new PriorityQueue<Car, double>(1);
        public CrossRoad(int id, double x, double y)
        {
            Id = id;
            X = x;
            Y = y;
            ArrivalCount = 0;
            Congestion = 0;
        }
        public void OnArrival(Car car)
        {
            // 외부에서 enqueue 할 때 호출: 단순히 카운트 증가
            ArrivalCount++;
            CarQueue.Enqueue(car);
        }
        public double GetHcost(CrossRoad goal)
        {
            //유클리드 거리
            double dx = goal.X - X;
            double dy = goal.Y - Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        public void InitProcessRate()
        {
            int effectiveLanes = Math.Min(Math.Max(1, IncomingRoads.Count), Math.Max(1, OutgoingRoads.Count)); //연결된 도로중 가장 적은 차선
            double baseFlowPerSec = 0.5; // 차선당 가능한 차량 처리 수
            SecondProcess = effectiveLanes * baseFlowPerSec;
        }
        public void CheckPriority()
        {
            Car car;
            double priority;

            while (CarQueue.Count != 0)
            {
                car = CarQueue.Dequeue();
                if (car.Goal == this)
                {
                    car.IsArrived = true;
                    continue;
                }
                else //차량이 목적지에 도달했으면 enqueue 하지 않음
                {
                    priority = car.Urgency + car.VisitTime;
                    PriorityQueue.Enqueue(car, priority);
                }
            }
        }
        public Road FindWay(Car car)
        {
            PriorityQueue<CrossRoad, double> pq = new PriorityQueue<CrossRoad, double>(-1);
            double fCost;
            Road res = null;

            foreach (Road road in OutgoingRoads)
            {
                if (road.To == car.Goal) return road;
                else
                {
                    fCost = road.GetGcost() + road.To.GetHcost(car.Goal);
                    pq.Enqueue(road.To, fCost);
                }
            }
            CrossRoad goal = pq.Dequeue();
            foreach (Road road in OutgoingRoads)
                if (road.To == goal) res = road;
            return res;
        }
        public void ServiceCar()
        {
            _maxProcess += SecondProcess;
            int allow = (int)_maxProcess;

            // allow 횟수만큼 반복하며 매번 새로운 차를 꺼냄
            while (allow > 0 && PriorityQueue.Count() > 0)
            {
                Car car = PriorityQueue.Dequeue();
                Road nextRoad = FindWay(car);

                if (nextRoad != null)
                {
                    car.VisitTime = 0; // 새 도로에 들어갔으니 체류시간 초기화
                    nextRoad.CarQueue.Enqueue(car);
                }
                allow--;
            }
            _maxProcess -= (int)_maxProcess; // 처리한 만큼 정수부만 뺌 (소수점 누적 유지)
        }
    }
    class Graph
    {
        public List<CrossRoad> Nodes { get; } = new List<CrossRoad>();
        public List<Road> Roads { get; } = new List<Road>();
        public List<Road> Entrys { get; } = new List<Road>();

        public CrossRoad AddCrossRoad(int id, double x, double y)
        {
            var cr = new CrossRoad(id,x,y);
            Nodes.Add(cr);
            return cr;
        }
        public Road AddRoad(CrossRoad from, CrossRoad to, double length, int lanes, int trafficLight, bool isEntry)
        {
            var road = new Road(from, to, length, lanes, trafficLight, isEntry);
            Roads.Add(road);
            if (isEntry) Entrys.Add(road);
            from.OutgoingRoads.Add(road);
            to.IncomingRoads.Add(road);
            return road;
        }
    }
}