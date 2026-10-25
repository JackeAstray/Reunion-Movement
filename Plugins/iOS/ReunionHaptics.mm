// ReunionMovement iOS 触觉实现。
// 与 Utils/Util/VibrationUtil.cs 里的 9 个 [DllImport("__Internal")] 一一对应。
// 放在 Plugins/iOS 下，Unity 只会在 iOS 构建时编译它。

#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator* selectionGenerator = nil;
static UIImpactFeedbackGenerator* lightImpactGenerator = nil;
static UIImpactFeedbackGenerator* mediumImpactGenerator = nil;
static UIImpactFeedbackGenerator* heavyImpactGenerator = nil;
static UINotificationFeedbackGenerator* notificationGenerator = nil;

extern "C" {

void InstantiateFeedbackGenerators(void)
{
    if (@available(iOS 10.0, *)) {
        if (selectionGenerator == nil) {
            selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
        }
        if (lightImpactGenerator == nil) {
            lightImpactGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        }
        if (mediumImpactGenerator == nil) {
            mediumImpactGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        }
        if (heavyImpactGenerator == nil) {
            heavyImpactGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        }
        if (notificationGenerator == nil) {
            notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
        }

        [selectionGenerator prepare];
        [lightImpactGenerator prepare];
        [mediumImpactGenerator prepare];
        [heavyImpactGenerator prepare];
        [notificationGenerator prepare];
    }
}

void ReleaseFeedbackGenerators(void)
{
    selectionGenerator = nil;
    lightImpactGenerator = nil;
    mediumImpactGenerator = nil;
    heavyImpactGenerator = nil;
    notificationGenerator = nil;
}

void SelectionHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [selectionGenerator selectionChanged];
    }
}

void SuccessHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [notificationGenerator notificationOccurred:UINotificationFeedbackTypeSuccess];
    }
}

void WarningHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [notificationGenerator notificationOccurred:UINotificationFeedbackTypeWarning];
    }
}

void FailureHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [notificationGenerator notificationOccurred:UINotificationFeedbackTypeError];
    }
}

void LightImpactHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [lightImpactGenerator impactOccurred];
    }
}

void MediumImpactHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [mediumImpactGenerator impactOccurred];
    }
}

void HeavyImpactHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        [heavyImpactGenerator impactOccurred];
    }
}

} // extern "C"
