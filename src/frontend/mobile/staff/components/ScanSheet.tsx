import { useRef } from 'react'
import { Linking, Text, View } from 'react-native'
import { CameraView, useCameraPermissions } from 'expo-camera'
import { useTranslation } from 'react-i18next'
import { Alert, BottomSheet, Button, Spinner } from '@/components/ui'

const SAME_CODE_PAUSE_MS = 2000

/**
 * Camera scanner for product barcodes (the barcode is the product's SKU). The same code is ignored for two seconds so one scan is not
 * counted five times; the sheet stays open so a cashier can scan the next item.
 */
export function ScanSheet({ visible, onClose, onCode }: { visible: boolean; onClose: () => void; onCode: (code: string) => void }) {
  const { t } = useTranslation()
  const [permission, requestPermission] = useCameraPermissions()
  const last = useRef<{ code: string; at: number }>({ code: '', at: 0 })

  function handle({ data }: { data: string }) {
    const now = Date.now()
    if (data === last.current.code && now - last.current.at < SAME_CODE_PAUSE_MS) return
    last.current = { code: data, at: now }
    onCode(data)
  }

  return (
    <BottomSheet visible={visible} title={t('pos.scanTitle')} onClose={onClose} closeLabel={t('common.close')}>
      {!permission ? (
        <View className="items-center py-8"><Spinner label={t('common.loading')} /></View>
      ) : !permission.granted ? (
        <View className="gap-3">
          <Alert tone="warning">{t(permission.canAskAgain ? 'pos.scanNeedPermission' : 'pos.scanDenied')}</Alert>
          {permission.canAskAgain ? (
            <Button size="lg" onPress={() => requestPermission()}>{t('pos.scanAllow')}</Button>
          ) : (
            <Button size="lg" variant="outline" onPress={() => Linking.openSettings()}>{t('pos.scanOpenSettings')}</Button>
          )}
        </View>
      ) : (
        <View className="gap-3">
          <View className="h-72 overflow-hidden rounded-card bg-ink">
            <CameraView
              style={{ flex: 1 }}
              facing="back"
              barcodeScannerSettings={{ barcodeTypes: ['ean13', 'ean8', 'upc_a', 'upc_e', 'code128', 'code39', 'qr'] }}
              onBarcodeScanned={handle}
              accessibilityLabel={t('pos.scanCamera')}
            />
          </View>
          <Text className="text-center text-sm text-ink-secondary">{t('pos.scanHint')}</Text>
          <Button size="lg" variant="outline" onPress={onClose}>{t('pos.scanDone')}</Button>
        </View>
      )}
    </BottomSheet>
  )
}
