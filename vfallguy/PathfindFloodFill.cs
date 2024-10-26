﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace vfallgay;

public class PathfindFloodFill
{
    public int NextT { get; private set; }
    private PathfindMap _map;
    private BitArray _solutions; // true if voxel is reachable
    private List<(int dx, int dy)> _moves = new();

    public bool Solution(int x, int y, int t) => _map.InBounds(x, y, t) ? _solutions[t * _map.Width * _map.Height + y * _map.Width + x] : false;
    public bool this[int x, int y, int t] => Solution(x, y, t);

    public PathfindFloodFill(PathfindMap map, float speed, int startX, int startY, int startT)
    {
        _map = map;
        _solutions = new(map.Voxels.Length);

        var r = speed * _map.TimeResolution * _map.InvSpaceResolution;
        var ir = (int)r;
        var rsq = r * r;
        var l = (int)(r / Math.Sqrt(2));
        _moves.Capacity = 5 + (l - 1) * 8;
        _moves.Add((0, 0)); // can stay still
        _moves.Add((0, ir)); // can move to cardinals
        _moves.Add((0, -ir));
        _moves.Add((ir, 0));
        _moves.Add((-ir, 0));
        for (int x = 1; x <= l; ++x)
        {
            var y = (int)Math.Sqrt(rsq - x * x);
            _moves.Add((x, y));
            _moves.Add((x, -y));
            _moves.Add((-x, y));
            _moves.Add((-x, -y));
            if (x != y)
            {
                _moves.Add((y, x));
                _moves.Add((y, -x));
                _moves.Add((-y, x));
                _moves.Add((-y, -x));
            }
        }

        if (_map.InBounds(startX, startY, startT))
            _solutions[startT * _map.Width * _map.Height + startY * _map.Width + startX] = true;
        NextT = startT + 1;
    }

    public (int t, int minY) ExecuteStep()
    {
        if (NextT >= _map.Duration)
            return (-1, 0);

        var tPrev = (NextT - 1) * _map.Width * _map.Height;
        var tNext = NextT * _map.Width * _map.Height;
        var minY = int.MaxValue;
        for (int y = 0; y < _map.Height; ++y)
        {
            for (int x = 0; x < _map.Width; ++x)
            {
                var xy = y * _map.Width + x;
                if (_map.Voxels[tNext + xy])
                    continue;

                foreach (var (dx, dy) in _moves)
                {
                    var x1 = x + dx;
                    var y1 = y + dy;
                    if (x1 >= 0 && x1 < _map.Width && y1 >= 0 && y1 < _map.Height && _solutions[tPrev + y1 * _map.Width + x1])
                    {
                        _solutions[tNext + xy] = true;
                        minY = Math.Min(minY, y);
                        break;
                    }
                }
            }
        }

        return (NextT++, minY);
    }

    public (int x, int y)? FindMove(int x, int y, int time)
    {
        var t = time - 1;
        if (!_map.InBounds(x, y, t))
            return null;

        var tPrev = t * _map.Width * _map.Height;
        foreach (var (dx, dy) in _moves)
        {
            var x1 = x + dx;
            var y1 = y + dy;
            if (x1 >= 0 && x1 < _map.Width && y1 >= 0 && y1 < _map.Height && _solutions[tPrev + y1 * _map.Width + x1])
            {
                return (x1, y1);
            }
        }

        return null;
    }

    // backwards
    public IEnumerable<(int x, int y, int t)> FindSolution(int x, int y, int t)
    {
        yield return (x, y, t);
        while (FindMove(x, y, t) is var move && move != null)
        {
            (x, y) = move.Value;
            --t;
            yield return (x, y, t);
        }
    }

    public IEnumerable<(int x, int y, int t)> SolveUntilY(int minY)
    {
        var t = DateTime.Now;
        while (ExecuteStep() is var res && res.t >= 0)
        {
            if (res.minY <= minY)
            {
                var minX = Enumerable.Range(0, _map.Width).First(x => this[x, res.minY, res.t]);
                Service.Log.Info($"Path rebuilt: took {(DateTime.Now - t).TotalMilliseconds:f3}ms");
                return FindSolution(minX, res.minY, res.t);
            }
        }
        return Enumerable.Empty<(int, int, int)>();
    }
}
