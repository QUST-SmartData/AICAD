using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AICAD
{
    public class Detection
    {
        public int Category { get; set; }

        public float Confidence { get; set; }

        public float X1 { get; set; }

        public float X2 { get; set; }

        public float Y1 { get; set; }

        public float Y2 { get; set; }
    }

    public class DetectionResponse
    {
        public List<Detection> Detections { get; set; }

        // 手动解析 JSON 字符串
        public static DetectionResponse Parse(string json)
        {
            DetectionResponse response = new DetectionResponse();
            response.Detections = new List<Detection>();

            // 定位到 "detections" 数组的开始
            int detectionsStart = json.IndexOf("[");
            int detectionsEnd = json.LastIndexOf("]");

            // 提取 "detections" 数组的内容
            string detectionsContent = json.Substring(detectionsStart + 1, detectionsEnd - detectionsStart - 1).Trim();

            if (detectionsContent!="")
            {
                // 分割每个检测结果
                string[] detectionItems = detectionsContent.Split(new[] { "},{" }, StringSplitOptions.None);

                foreach (var item in detectionItems)
                {
                    Detection detection = new Detection();

                    // 处理 item 字符串，去掉首尾的大括号
                    string cleanItem = item.Trim(new char[] { '{', '}' });

                    // 分割每个键值对
                    string[] properties = cleanItem.Split(',');

                    foreach (var prop in properties)
                    {
                        // 拆分键和值
                        string[] keyValue = prop.Split(':');
                        string key = keyValue[0].Trim(new char[] { '"', ' ' });
                        string value = keyValue[1].Trim();

                        // 根据键名赋值
                        switch (key)
                        {
                            case "category":
                                detection.Category = int.Parse(value);
                                break;
                            case "confidence":
                                detection.Confidence = float.Parse(value);
                                break;
                            case "x1":
                                detection.X1 = float.Parse(value);
                                break;
                            case "x2":
                                detection.X2 = float.Parse(value);
                                break;
                            case "y1":
                                detection.Y1 = float.Parse(value);
                                break;
                            case "y2":
                                detection.Y2 = float.Parse(value);
                                break;
                        }
                    }

                    // 将解析好的 Detection 对象添加到列表中
                    response.Detections.Add(detection);
                }
            }
            

            return response;
        }
    }
}
