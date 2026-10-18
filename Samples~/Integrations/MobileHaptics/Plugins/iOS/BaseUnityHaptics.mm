#import <UIKit/UIKit.h>
extern "C" void BaseUnityPlayHaptic(int kind) {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 10.0, *)) {
            if (kind >= 3) {
                UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
                [generator prepare];
                [generator notificationOccurred:kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError];
            } else {
                UIImpactFeedbackStyle style = kind == 0 ? UIImpactFeedbackStyleLight : kind == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
                UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
                [generator prepare]; [generator impactOccurred];
            }
        }
    });
}
