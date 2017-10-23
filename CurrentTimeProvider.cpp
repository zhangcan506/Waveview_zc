#pragma once  

#include <sys/timeb.h>  
#include <time.h>  
#include <Windows.h>  
#include <akumaslab/system/singleton.hpp>  


/*
����˼·���£�

1���߾��ȶ�ʱ����ʹ��Singletonģʽ��������Sleep���̲߳�ͳһ������̨ʹ��Windows MultiMedia SDK�Ķ��ڻص��������ϼ�Ⲣ�ظ���ʱ���̣߳�
��ʱʱ���뵱ǰʱ�����QueryPerformanceCounter/QueryPerformanceFrequency�ĸ߾��ȼ�ʱ��ȷ�����幦�ܿɿ��ԡ�

2����ȷʱ�̻�ȡ�����ڿ��Ի�ȡ�����뼶���_ftime��GetTickCount���ܵ�Windowsϵͳʱ�侫��Ӱ�죬��С��λֻ��15ms��
������Ҫ����QueryPerformanceCounter/QueryPerformanceFrequency����׼ȷ��ʱ���������ȸ���_ftime��ȡ��ʼʱ�̵ľ�ȷ�̶ȣ�
Ȼ����ݲ������㵱ǰ�ľ�ȷʱ�̡�
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