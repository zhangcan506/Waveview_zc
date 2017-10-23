#pragma once  

#include <sys/timeb.h>  
#include <time.h>  
#include <Windows.h>  
#include <akumaslab/system/singleton.hpp>  


/*
代码思路如下：

1、高精度定时器。使用Singleton模式挂起请求Sleep的线程并统一管理，后台使用Windows MultiMedia SDK的定期回调函数不断检测并回复到时的线程，
超时时间与当前时间采用QueryPerformanceCounter/QueryPerformanceFrequency的高精度计时，确保整体功能可靠性。

2、精确时刻获取。由于可以获取到毫秒级别的_ftime与GetTickCount都受到Windows系统时间精度影响，最小单位只有15ms，
所以需要借助QueryPerformanceCounter/QueryPerformanceFrequency进行准确计时。代码首先根据_ftime获取起始时刻的精确刻度，
然后根据差量计算当前的精确时刻。
*/
namespace akumaslab {
	namespace time {
		struct HighResolutionTime
		{
			int year;
			int month;
			int day;
			int hour;
			int min;
			int second;
			int millisecond;
		};
		class CurrentTimeProvider
		{
		public:
			CurrentTimeProvider() :highResolutionAvailable(false), countPerMilliSecond(0), beginCount(0)
			{
				static LARGE_INTEGER systemFrequency;
				if (0 != QueryPerformanceFrequency(&systemFrequency))
				{
					highResolutionAvailable = true;
					countPerMilliSecond = systemFrequency.QuadPart / 1000;
					_timeb tb;
					_ftime_s(&tb);
					unsigned short currentMilli = tb.millitm;
					LARGE_INTEGER now;
					QueryPerformanceCounter(&now);
					beginCount = now.QuadPart - (currentMilli*countPerMilliSecond);
				}
			};
			bool getCurrentTime(HighResolutionTime& _time)
			{
				time_t tt;
				::time(&tt);
				tm now;
				localtime_s(&now, &tt);
				_time.year = now.tm_year + 1900;
				_time.month = now.tm_mon + 1;
				_time.day = now.tm_mday + 1;
				_time.hour = now.tm_hour;
				_time.min = now.tm_min;
				_time.second = now.tm_sec;
				if (!highResolutionAvailable)
				{
					_time.millisecond = 0;
				}
				else {
					LARGE_INTEGER qfc;
					QueryPerformanceCounter(&qfc);
					_time.millisecond = (int)((qfc.QuadPart - beginCount) / countPerMilliSecond) % 1000;
				}
				return true;
			}
		private:
			bool highResolutionAvailable;
			LONGLONG countPerMilliSecond;
			LONGLONG beginCount;
		};
		class CurrentTime
		{
		public:
			static bool get(HighResolutionTime& _time)
			{
				return akumaslab::system::Singleton< CurrentTimeProvider >::getRef().getCurrentTime(_time);
			}
		};
	}
}