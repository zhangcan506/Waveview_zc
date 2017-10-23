#pragma once  

#include <Windows.h>  
#include <list>  
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
		using std::list;

		class PreciseTimerProvider
		{
			struct WaitedHandle {
				HANDLE threadHandle;
				LONGLONG elapsed;//超时时间  
			};
			typedef list< WaitedHandle > handle_list_type;
			typedef akumaslab::system::Singleton< PreciseTimerProvider > timer_type;
		public:
			PreciseTimerProvider(void) :highResolutionAvailable(false), timerID(0)
			{
				InitializeCriticalSection(&critical);
				static LARGE_INTEGER systemFrequency;
				if (0 != QueryPerformanceFrequency(&systemFrequency))
				{
					timeBeginPeriod(callbackInterval);
					highResolutionAvailable = true;
					countPerMilliSecond = systemFrequency.QuadPart / 1000;
					timerID = timeSetEvent(callbackInterval, 0, &PreciseTimerProvider::TimerFunc, NULL, TIME_PERIODIC);
				}
			}
			//挂起当前线程  
			//@milliSecond:超时时间，单位：毫秒  
			bool suspendCurrentThread(int milliSecond)
			{
				if (milliSecond <= 0)return false;
				if (!highResolutionAvailable)return false;
				HANDLE currentThreadHandle = GetCurrentThread();
				HANDLE currentProcessHandle = GetCurrentProcess();
				HANDLE realThreadHandle(0);
				DuplicateHandle(currentProcessHandle, currentThreadHandle, currentProcessHandle, &realThreadHandle, 0, FALSE, DUPLICATE_SAME_ACCESS);
				WaitedHandle item;
				item.threadHandle = realThreadHandle;
				LARGE_INTEGER now;
				QueryPerformanceCounter(&now);
				now.QuadPart += milliSecond * countPerMilliSecond;
				item.elapsed = now.QuadPart;
				EnterCriticalSection(&critical);
				waitList.push_back(item);
				LeaveCriticalSection(&critical);
				//挂起线程  
				SuspendThread(realThreadHandle);
				CloseHandle(realThreadHandle);
				return true;
			}
			//恢复超时线程  
			void resumeTimeoutThread()
			{
				if (!highResolutionAvailable)return;
				LARGE_INTEGER now;
				QueryPerformanceCounter(&now);
				EnterCriticalSection(&critical);
				for (handle_list_type::iterator ir = waitList.begin(); ir != waitList.end(); )
				{
					WaitedHandle& waited = *ir;
					if (now.QuadPart >= waited.elapsed)
					{
						ResumeThread(waited.threadHandle);
						ir = waitList.erase(ir);
						continue;
					}
					ir++;
				}
				LeaveCriticalSection(&critical);
			}
			~PreciseTimerProvider() {
				if (0 != timerID)
				{
					timeKillEvent(timerID);
					timerID = 0;
					timeEndPeriod(callbackInterval);
				}
				DeleteCriticalSection(&critical);
			}
		private:

			static void CALLBACK TimerFunc(UINT uID, UINT uMsg, DWORD dwUser, DWORD dw1, DWORD dw2)
			{
				static bool initialed = false;
				if (!initialed)
				{
					if (initialWorkThread())
					{
						initialed = true;
					}
					else {
						return;
					}
				}
				timer_type::getRef().resumeTimeoutThread();
			}
			//调整定时器工作线程优先级  
			static bool initialWorkThread()
			{
				HANDLE realProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, _getpid());
				if (NULL == realProcessHandle)
				{
					return false;
				}
				if (0 == SetPriorityClass(realProcessHandle, REALTIME_PRIORITY_CLASS))
				{
					CloseHandle(realProcessHandle);
					return false;
				}
				HANDLE currentThreadHandle = GetCurrentThread();
				HANDLE currentProcessHandle = GetCurrentProcess();
				HANDLE realThreadHandle(0);
				DuplicateHandle(currentProcessHandle, currentThreadHandle, currentProcessHandle, &realThreadHandle, 0, FALSE, DUPLICATE_SAME_ACCESS);
				SetThreadPriority(realThreadHandle, THREAD_PRIORITY_TIME_CRITICAL);
				//必须关闭复制句柄  
				CloseHandle(realThreadHandle);
				CloseHandle(realProcessHandle);
				return true;
			}
		private:
			const static int callbackInterval = 1;
			CRITICAL_SECTION critical;
			MMRESULT timerID;
			LONGLONG countPerMilliSecond;
			bool highResolutionAvailable;
			handle_list_type waitList;
		};
		class PreciseTimer
		{
			typedef akumaslab::system::Singleton< PreciseTimerProvider > timer_type;
		public:
			static bool wait(int milliSecond)
			{
				//static PreciseTimerProvider timer;  
				return timer_type::getRef().suspendCurrentThread(milliSecond);
			}
		};
	}
}