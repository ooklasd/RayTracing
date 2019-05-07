# 说明
* 目前只在update生成射线并计算，也就是单线程，每帧计算300个像素。
* 可以使用Loom在主线程完成碰撞检测，其他线程运算颜色的形式实现多线程计算。
* 漫反射追踪计算量惊人。
* 漫反射，高光，折射之间并没有好好处理。

# 效果图
![效果图](https://raw.githubusercontent.com/ooklasd/RayTracing/master/renderingTemp.jpg "采样4X4,递归层次10,单线程渲染时间0:8:12")
