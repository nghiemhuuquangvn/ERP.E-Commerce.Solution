import { describe, it, expect, afterEach } from 'vitest'
import { defineComponent, nextTick, ref } from 'vue'
import { mount, flushPromises } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { axe } from '@/test/axe'
import { testI18n } from '@/test/setup'
import { useToast } from '@/composables/useToast'
import Alert from './Alert.vue'
import Button from './Button.vue'
import Checkbox from './Checkbox.vue'
import Drawer from './Drawer.vue'
import EmptyState from './EmptyState.vue'
import FormField from './FormField.vue'
import Input from './Input.vue'
import Pagination from './Pagination.vue'
import PriceText from './PriceText.vue'
import QuantityStepper from './QuantityStepper.vue'
import Select from './Select.vue'
import Stepper from './Stepper.vue'
import ToastHost from './ToastHost.vue'

const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }, { path: '/cart', component: { template: '<div />' } }] })
const mounted: ReturnType<typeof mount>[] = []
const mountIt = (component: object, options: Record<string, unknown> = {}) => {
  const w = mount(component, { attachTo: document.body, global: { plugins: [testI18n, router] }, ...options })
  mounted.push(w)
  return w
}
afterEach(() => {
  while (mounted.length) mounted.pop()!.unmount()
  document.body.innerHTML = ''
  document.body.style.overflow = ''
})

describe('FormField + controls', () => {
  it('links the label, the hint and the error to the control and marks it invalid', async () => {
    const value = ref('')
    const Host = defineComponent({
      components: { FormField, Input },
      setup: () => ({ value, error: ref('') }),
      template: '<main><FormField label="Phone" hint="The carrier calls it" :error="error" required><Input v-model="value" /></FormField><button @click="error = \'Bắt buộc\'">fail</button></main>',
    })
    const w = mountIt(Host)
    const input = w.get('input')
    expect(w.get('label').attributes('for')).toBe(input.attributes('id'))
    expect(input.attributes('aria-required')).toBe('true')
    expect(document.getElementById(input.attributes('aria-describedby')!)!.textContent).toContain('The carrier calls it')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()

    await w.get('button').trigger('click')
    expect(input.attributes('aria-invalid')).toBe('true')
    expect(document.getElementById(input.attributes('aria-describedby')!)!.textContent).toBe('Bắt buộc')
    expect(w.get('[role=alert]').text()).toBe('Bắt buộc')

    await input.setValue('0901234567')
    expect(value.value).toBe('0901234567')
  })

  it('Select and Checkbox keep their v-model', async () => {
    const choice = ref('a')
    const on = ref(false)
    const Host = defineComponent({
      components: { FormField, Select, Checkbox },
      setup: () => ({ choice, on }),
      template: '<main><FormField label="Sort"><Select v-model="choice"><option value="a">A</option><option value="b">B</option></Select></FormField><Checkbox v-model="on" label="Save address" /></main>',
    })
    const w = mountIt(Host)
    await w.get('select').setValue('b')
    expect(choice.value).toBe('b')
    await w.get('input[type=checkbox]').setValue(true)
    expect(on.value).toBe(true)
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })
})

describe('Button', () => {
  it('shows progress and ignores clicks while loading, renders a link when given a route', async () => {
    const w = mountIt(Button, { props: { loading: true }, slots: { default: 'Pay' } })
    expect(w.get('button').attributes('disabled')).toBeDefined()
    expect(w.get('button').attributes('aria-busy')).toBe('true')
    const link = mountIt(Button, { props: { to: '/cart' }, slots: { default: 'Cart' } })
    expect(link.get('a').attributes('href')).toBe('/cart')
  })
})

describe('Alert, EmptyState, PriceText', () => {
  it('a danger alert is announced at once, the others politely', () => {
    expect(mountIt(Alert, { props: { variant: 'danger' }, slots: { default: 'Refused' } }).get('[role=alert]').text()).toBe('Refused')
    expect(mountIt(Alert, { props: { variant: 'success' }, slots: { default: 'Done' } }).get('[role=status]').text()).toBe('Done')
  })

  it('an empty state says what happened and offers the next step', async () => {
    const w = mountIt(EmptyState, { props: { title: 'No products found.', description: 'Try another word.' }, slots: { default: '<button>Clear</button>' } })
    expect(w.text()).toContain('No products found.')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })

  it('shows the discount, the old price struck through and "free" in words', () => {
    const w = mountIt(PriceText, { props: { amount: 80, oldAmount: 100 } })
    expect(w.text()).toContain('$80.00')
    expect(w.get('s').text()).toBe('$100.00')
    expect(w.text()).toContain('−20%')
    expect(mountIt(PriceText, { props: { amount: 0, free: true, freeLabel: 'Free delivery' } }).text()).toContain('Free delivery')
  })
})

describe('QuantityStepper', () => {
  it('has named buttons, respects the minimum and the maximum', async () => {
    const w = mountIt(QuantityStepper, { props: { modelValue: 1, max: 2, label: 'Quantity', decreaseLabel: 'Fewer', increaseLabel: 'More' } })
    expect(w.get('[aria-label=Fewer]').attributes('disabled')).toBeDefined()
    await w.get('[aria-label=More]').trigger('click')
    expect(w.emitted('update:modelValue')![0]).toEqual([2])
    await w.setProps({ modelValue: 2 } as never)
    expect(w.get('[aria-label=More]').attributes('disabled')).toBeDefined()
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })
})

describe('Stepper and Pagination', () => {
  it('marks the current step and says which are done', async () => {
    const w = mountIt(Stepper, { props: { steps: ['Shipping', 'Payment', 'Review'], current: 2, label: 'Checkout steps' } })
    const items = w.findAll('li[aria-current]')
    expect(items).toHaveLength(1)
    expect(items[0].text()).toContain('Payment')
    expect(await axe(w.element as HTMLElement)).toHaveNoViolations()
  })

  it('moves between pages and disables the ends', async () => {
    const w = mountIt(Pagination, { props: { page: 1, totalPages: 3, label: 'Pages', prevLabel: 'Prev', nextLabel: 'Next', pageLabel: 'Page 1 of 3' } })
    const [prev, next] = w.findAll('button')
    expect(prev.attributes('disabled')).toBeDefined()
    await next.trigger('click')
    expect(w.emitted('update:page')![0]).toEqual([2])
    expect(mountIt(Pagination, { props: { page: 1, totalPages: 1, label: 'Pages', prevLabel: 'Prev', nextLabel: 'Next', pageLabel: '' } }).find('nav').exists()).toBe(false)
  })
})

describe('Drawer', () => {
  it('traps focus, closes on Esc, locks the scroll and returns the focus to the opener', async () => {
    const open = ref(false)
    const Host = defineComponent({
      components: { Drawer },
      setup: () => ({ open }),
      template: '<div><button id="opener" @click="open = true">Menu</button><Drawer :open="open" title="Menu" close-label="Close" @close="open = false"><a href="/x">Products</a></Drawer></div>',
    })
    const w = mountIt(Host)
    const opener = w.get('#opener').element as HTMLButtonElement
    opener.focus()
    await opener.click()
    await flushPromises()
    await nextTick()
    const dialog = document.querySelector('[role=dialog]') as HTMLElement
    expect(dialog).toBeTruthy()
    expect(dialog.contains(document.activeElement)).toBe(true)
    expect(document.body.style.overflow).toBe('hidden')
    expect(await axe(dialog)).toHaveNoViolations()

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }))
    await flushPromises()
    expect(document.querySelector('[role=dialog]')).toBeNull()
    expect(document.body.style.overflow).toBe('')
    expect(document.activeElement).toBe(opener)
  })
})

describe('Toast', () => {
  it('a success message is announced politely and can be closed, an error is an alert', async () => {
    const w = mountIt(ToastHost)
    const toast = useToast()
    toast.success('Added to cart')
    toast.error('Could not add')
    await nextTick()
    expect(w.get('[role=status]').text()).toContain('Added to cart')
    expect(w.get('[role=alert]').text()).toContain('Could not add')
    await w.get('[role=status] button').trigger('click')
    expect(w.find('[role=status]').exists()).toBe(false)
    for (const t of [...toast.toasts.value]) toast.dismiss(t.id)
  })
})
