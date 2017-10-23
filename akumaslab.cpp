#pragma once  

#include <Windows.h>  
#include <list>  
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
		using std::list;

		class PreciseTimerProvider
		{
			struct WaitedHandle {
				HANDLE threadHandle;
				LONGLONG elapsed;//��ʱʱ��  
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
			//����ǰ�߳�  
			//@milliSecond:��ʱʱ�䣬��λ������  
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
				//�����߳�  
				SuspendThread(realThreadHandle);
				CloseHandle(realThreadHandle);
				return true;
			}
			//�ָ���ʱ�߳�  
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
			//������ʱ�������߳����ȼ�  
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
				//����رո��ƾ��  
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