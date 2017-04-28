﻿// Created by Sebastian Mueller (smueller@ifi.uzh.ch) from the University of Zurich
// Created: 2017-04-12
// 
// Licensed under the MIT License.

using System;
using GoalSetting.Goals;
using Shared;
using Shared.Helpers;
using System.Collections.Generic;
using GoalSetting.Model;
using System.Linq;
using Shared.Data;
using GoalSetting.Data;

namespace GoalSetting.Visualizers.Week
{
    internal class WeekVisualizationForWeeklyGoal : PAVisualization
    {
        public WeekVisualizationForWeeklyGoal(DateTimeOffset date, GoalActivity goal) : base(date, goal) {
            Order = Int32.MinValue;
        }

        public override string GetHtml()
        {
            var html = string.Empty;
            var startOfWork = DateTimeHelper.GetFirstDayOfWeek_Iso8801(_date);
            var endOfWork = DateTime.Now;
            
            var activities = DatabaseConnector.GetActivitiesSinceAndBefore(startOfWork.DateTime, endOfWork);
            activities = DataHelper.MergeSameActivities(activities, Settings.MinimumSwitchTimeInSeconds);

            var targetActivity = _goal.Activity;

            // CSS
            html += "<style type='text/css'>";
            html += ".c3-line { stroke-width: 2px; }";
            html += ".c3-grid text, c3.grid line { fill: black; }";
            html += ".axis path, .axis line {fill: none; stroke: black; stroke-width: 1; shape-rendering: crispEdges;}";
            html += "</style>";

            //HTML
            html += "<div id='" + VisHelper.CreateChartHtmlTitle(Title) + "' style='align: center'></div>";
            html += "<p style='text-align: center; font-size: 0.66em;'>" + _goal.GetProgressMessage() + "</p>";

            //JS
            html += "<script>";

            //Calculate size of visualization
            html += "var actualHeight = document.getElementsByClassName('item Wide')[0].offsetHeight;";
            html += "var actualWidth = document.getElementsByClassName('item Wide')[0].offsetWidth;";
            html += "var margin = {top: 30, right: 30, bottom: 30, left: 40}, width = (actualWidth * 0.97)- margin.left - margin.right, height = (actualHeight * 0.73) - margin.top - margin.bottom;";

            //Prepare data
            html += "var parseTime = d3.time.format('%Y-%m-%d %H:%M').parse;";
            var dataPoints = GenerateData(activities, targetActivity, startOfWork.DateTime);
            html += GenerateJSData(dataPoints);
            html += "data.forEach(function(d) {d.start = parseTime(d.start); d.end = parseTime(d.end);});";

            //Prepare scales
            html += "var x = d3.time.scale().range([0, width]);";
            html += "var y0 = d3.scale.linear().range([height, 0]);";

            //Prepare axis
            html += "var xAxis = d3.svg.axis().scale(x).orient('bottom').tickFormat(d3.time.format('%a (%H:%M)')).ticks(4);";
            html += "var yAxisLeft = d3.svg.axis().scale(y0).orient('left').ticks(5);";

            string color1 = string.Empty;
            string color2 = string.Empty;
            string pattern1 = string.Empty;
            string pattern2 = string.Empty;

            switch (_goal.Rule.Operator)
            {
                case RuleOperator.Equal:
                    color1 = GoalVisHelper.GetVeryLowColor();
                    color2 = GoalVisHelper.GetVeryLowColor();
                    pattern1 = "#error-pattern";
                    pattern2 = "#error-pattern";
                    break;
                case RuleOperator.LessThan:
                    color1 = GoalVisHelper.GetVeryHighColor();
                    color2 = GoalVisHelper.GetVeryLowColor();
                    pattern1 = "#success-pattern";
                    pattern2 = "#error-pattern";
                    break;
                case RuleOperator.GreaterThan:
                    color1 = GoalVisHelper.GetVeryLowColor();
                    color2 = GoalVisHelper.GetVeryHighColor();
                    pattern1 = "#error-pattern";
                    pattern2 = "#success-pattern";
                    break;
            }

            //Prepare lines
            if (_goal.Rule.Goal == RuleGoal.TimeSpentOn)
            {
                html += "var limit = " + TimeSpan.FromMilliseconds(double.Parse(_goal.Rule.TargetValue)).TotalHours + ";";
            }
            else if (_goal.Rule.Goal == RuleGoal.NumberOfSwitchesTo)
            {
                html += "var limit = " + double.Parse(_goal.Rule.TargetValue) + ";";
            }
            html += "var valueLine1 = d3.svg.line().interpolate('step-after').x(function(d) {return x(d.start); }).y(function(d) { return y0(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + "); });";

            //Prepare chart area
            html += "var svg = d3.select('#" + VisHelper.CreateChartHtmlTitle(Title) + "').append('svg').attr('width', width + margin.left + margin.right).attr('height', height + margin.top + margin.bottom).append('g').attr('transform', 'translate(' + margin.left + ',' + margin.top + ')');";

            //Prepare patterns to color rectangles
            html += @"var pattern = svg.append('defs')
                    .append('pattern')
                    .attr({ id: 'success-pattern', width: '8', height: '8', patternUnits: 'userSpaceOnUse', patternTransform: 'rotate(60)'})
	                .append('rect')
                    .attr({ width: '4', height: '8', transform: 'translate(0,0)', opacity: '0.25', fill: '" + GoalVisHelper.GetVeryHighColor() + "' });";

            html += @"var pattern = svg.append('defs')
                    .append('pattern')
                    .attr({ id: 'error-pattern', width: '8', height: '8', patternUnits: 'userSpaceOnUse', patternTransform: 'rotate(60)'})
	                .append('rect')
                    .attr({ width: '4', height: '8', transform: 'translate(0,0)', opacity: '0.25', fill: '" + GoalVisHelper.GetVeryLowColor() + "' });";

            //Prepare domain of axes
            if (dataPoints.Count > 0)
            {
                html += "x.domain( [d3.min(data, function(d) { return d.start; }) , d3.max(data, function(d) { return d.end; }) ] );";
                html += "var switchValues = data.map(function(d){return " + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + ";}).filter(function(val) {return val !== null});";
                html += "var timeValues = data.map(function(o){return o.time;}).filter(function(val) {return val !== null});";
                html += "y0.domain([0, d3.max(data, function(d) {return Math.max(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + ");}) * 1.01]);";
            }
            else
            {
                html += "y0.domain([0,0]);";
                html += "x.domain([parseTime('" + Database.GetInstance().GetUserWorkStart(DateTime.Now.Date).ToString("HH:mm") + "'), parseTime('" + Database.GetInstance().GetUserWorkEnd(DateTime.Now.Date).ToString("HH:mm") + "')]);";
            }

            //Draw lines and axes
            html += "svg.append('path').style('stroke', '" + color1 + "').attr('d', valueLine1(data)).attr('fill', 'none').attr('stroke-width', '3');";
            html += "svg.append('path').style('stroke', '" + color2 + "').attr('d', valueLine1(data.filter(function(d) {return " + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + " >= limit;}))).attr('fill', 'none').attr('stroke-width', '3');";
            html += "xAxisYPosition = height;";
            html += "svg.append('g').attr('class', 'x axis').attr('transform', 'translate(0,' + xAxisYPosition + ')').call(xAxis);";
            html += "svg.append('g').attr('class', 'y axis').style('fill', 'black').call(yAxisLeft);";
            html += "svg.append('line').style('stroke-dasharray', ('3, 3')).style('stroke', '" + Shared.Settings.RetrospectionColorHex + "').attr('x1', 0).attr('y1', y0(limit)).attr('x2', d3.max(data, function(d){return x(d.start);})).attr('y2', y0(limit));";

            //Draw legend
            html += "svg.append('text').attr('x', 0).attr('y', -10).style('text-anchor', 'middle').style('font-size', '0.5em').text('" + GoalVisHelper.GetXAxisTitle(_goal, VisType.Day) + "');";

            //Draw hatched rectangles
            html += "svg.append('g')";
            html += ".attr('id', 'bars')";
            html += ".selectAll('rect')";
            html += ".data(data.filter(function(d){return " + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + " <= limit;}))";
            html += ".enter()";
            html += ".append('rect')";
            html += ".attr({'x':function(d) {return x(d.start);},'y':function(d){ return y0(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + "); } })";
            html += ".style('fill', 'url(" + pattern1 + ")')";
            html += ".attr('height', function(d) {return xAxisYPosition - y0(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + ");})";
            html += ".attr('width', function(d){ return x(d.end) - x(d.start); });";

            html += "svg.append('g')";
            html += ".attr('id', 'bars')";
            html += ".selectAll('rect')";
            html += ".data(data.filter(function(d){return " + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + " > limit;}))";
            html += ".enter()";
            html += ".append('rect')";
            html += ".attr({'x':function(d) {return x(d.start);},'y':function(d){ return y0(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + "); } })";
            html += ".style('fill', 'url(" + pattern2 + ")')";
            html += ".attr('height', function(d) {return xAxisYPosition - y0(" + GoalVisHelper.GetDataPointName(_goal, VisType.Day) + ");})";
            html += ".attr('width', function(d){ return x(d.end) - x(d.start); });";

            html += "</script>";

            return html;
        }

        private List<TimelineDataPoint> GenerateData(List<ActivityContext> activities, ContextCategory targetActivity, DateTime start)
        {
            TimeSpan sumTime = TimeSpan.FromMilliseconds(0);
            int sumSwitches = 0;

            var dataPoints = new List<TimelineDataPoint>();

            for (int i = 0; i < activities.Count - 1; i++)
            {
                activities.ElementAt(i).End = activities.ElementAt(i + 1).Start;
            }

            activities.RemoveAll(a => a.Activity != targetActivity);

            foreach (var activity in activities)
            {
                if (activity.Start.Day == activity.End.Value.Day - 1 && activity.Duration.TotalHours > 6)
                {
                    continue;
                }

                sumSwitches += 1;

                //Iterate over each minute of the activity to generate a data point for each minute
                var startDate = activity.Start;
                var timeCountedInLoop = TimeSpan.FromMinutes(0);

                while (startDate < activity.End)
                {
                    sumTime += TimeSpan.FromMinutes(1);
                    timeCountedInLoop += TimeSpan.FromMinutes(1);
                    dataPoints.Add(new TimelineDataPoint { Start = startDate, End = startDate.AddMinutes(1), SumTime = sumTime, SumSwitches = sumSwitches });
                    startDate = startDate.AddMinutes(1);
                }

            }

            //if we have actual data points, add 1 more datapoint at the end of the list with the current data to ensure that the line is drawn until now.
            if (dataPoints.Count > 0)
            {
                dataPoints.Insert(0, new TimelineDataPoint { Start = start, End = start, SumSwitches = 0, SumTime = TimeSpan.FromTicks(9) });
                dataPoints.Add(new TimelineDataPoint { Start = DateTime.Now, End = DateTime.Now, SumTime = sumTime, SumSwitches = sumSwitches });
            }

            return dataPoints;
        }

        private String GenerateJSData(List<TimelineDataPoint> dataPoints)
        {
            string data = "var data = [";

            bool hasData = false;
            foreach (TimelineDataPoint dataPoint in dataPoints)
            {
                data += dataPoint + ",";
                hasData = true;
            }

            if (hasData)
            {
                data = data.Substring(0, data.Length - 1);
            }
            data += "];";

            return data;
        }

        public struct TimelineDataPoint
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public TimeSpan SumTime { get; set; }
            public int SumSwitches { get; set; }

            public override string ToString()
            {
                return "{'start':'" + Start.ToString("yyyy-MM-dd HH:mm") + "', 'end': '" + End.ToString("yyyy-MM-dd HH:mm") + "', 'time': " + SumTime.TotalHours + ", 'switch': " + SumSwitches + "}";
            }
        }
    }
}