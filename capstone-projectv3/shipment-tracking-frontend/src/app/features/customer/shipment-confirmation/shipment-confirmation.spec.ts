import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ShipmentConfirmation } from './shipment-confirmation';

describe('ShipmentConfirmation', () => {
  let component: ShipmentConfirmation;
  let fixture: ComponentFixture<ShipmentConfirmation>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ShipmentConfirmation],
    }).compileComponents();

    fixture = TestBed.createComponent(ShipmentConfirmation);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
