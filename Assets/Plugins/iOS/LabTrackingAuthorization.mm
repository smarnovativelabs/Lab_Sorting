#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UIKit/UIKit.h>

extern "C" int LabTrackingStatus() {
    if (@available(iOS 14, *)) return (int)[ATTrackingManager trackingAuthorizationStatus];
    return 2;
}
extern "C" void LabRequestTracking() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 14, *)) {
            if ([UIApplication sharedApplication].applicationState == UIApplicationStateActive &&
                [ATTrackingManager trackingAuthorizationStatus] == ATTrackingManagerAuthorizationStatusNotDetermined) {
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {}];
            }
        }
    });
}
